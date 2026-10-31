local _, T = ...
local Addon, Plain = T.Addon, T.Plain

local Snapshot = {}
T.Snapshot = Snapshot

local UPDATE_EVENTS = {
    "CHALLENGE_MODE_MAPS_UPDATE",
    "MYTHIC_PLUS_CURRENT_AFFIX_UPDATE",
    "WEEKLY_REWARDS_UPDATE",
    "BAG_UPDATE_DELAYED",
    "CHALLENGE_MODE_COMPLETED",
    "PLAYER_ENTERING_WORLD",
}

function Snapshot:Start()
    C_MythicPlus.RequestMapInfo()
    C_MythicPlus.RequestCurrentAffixes()
    C_MythicPlus.RequestRewards()
    for _, event in ipairs(UPDATE_EVENTS) do
        Addon:On(event, function() self:Schedule() end)
    end
    Addon:On("PLAYER_LOGOUT", function() Addon:Flush("snapshot") end)
    self:Schedule()
end

function Snapshot:Schedule()
    Addon:Debounce("snapshot", 2, function() self:Take() end)
end

local RETRY_EVERY, RETRY_MAX = 5, 12
local retries = 0

local function retryLater()
    if retries >= RETRY_MAX then return end
    retries = retries + 1
    C_MythicPlus.RequestMapInfo()
    C_Timer.After(RETRY_EVERY, function() Snapshot:Schedule() end)
end

local function consistent(rating, best)
    return not (rating and rating > 0) or next(best) ~= nil
end

function Snapshot:OwnKey()
    local mapId = Plain(C_MythicPlus.GetOwnedKeystoneChallengeMapID())
    local level = Plain(C_MythicPlus.GetOwnedKeystoneLevel())
    if mapId and level and level > 0 then return mapId, level end
    return nil
end

function Snapshot:Rating()
    local rating = Plain(C_ChallengeMode.GetOverallDungeonScore())
    if not rating or rating == 0 then
        local summary = C_PlayerInfo and C_PlayerInfo.GetPlayerMythicPlusRatingSummary
            and C_PlayerInfo.GetPlayerMythicPlusRatingSummary("player")
        rating = summary and Plain(summary.currentSeasonScore)
    end
    return rating or 0
end

local REWARD_LEVELS = 30

function Snapshot.RewardLevels()
    local rewards = {}
    if not C_MythicPlus.GetRewardLevelForDifficultyLevel then return rewards end
    for level = 2, REWARD_LEVELS do
        local vault, endOfRun = C_MythicPlus.GetRewardLevelForDifficultyLevel(level)
        vault, endOfRun = Plain(vault), Plain(endOfRun)
        if not (endOfRun and endOfRun > 0) and C_MythicPlus.GetRewardLevelFromKeystoneLevel then
            endOfRun = Plain(C_MythicPlus.GetRewardLevelFromKeystoneLevel(level))
        end
        local itemLevel = (endOfRun and endOfRun > 0) and endOfRun or vault
        if itemLevel and itemLevel > 0 then
            rewards[level] = {
                itemLevel = itemLevel,
                vaultItemLevel = (vault and vault > 0 and vault ~= itemLevel) and vault or nil,
            }
        end
    end
    return rewards
end

local REWARD_RETRIES = 12
local rewardRetries = 0

local function rewardLevels(db)
    local rewards = Snapshot.RewardLevels()
    if next(rewards) then
        db.rewards = rewards
        rewardRetries = 0
        return
    end
    if db.rewards or rewardRetries >= REWARD_RETRIES then return end
    rewardRetries = rewardRetries + 1
    C_MythicPlus.RequestRewards()
    C_Timer.After(5, function() Snapshot:Schedule() end)
end

local function seasonData(db)
    db.region = Plain(GetCurrentRegionName()) or db.region
    local season = Plain(C_MythicPlus.GetCurrentSeason())
    if season and season > 0 then
        if db.season and db.season ~= season then db.rewards = nil end
        db.season = season
    end

    local untilReset = Plain(C_DateAndTime.GetSecondsUntilWeeklyReset())
    if untilReset then db.resetAt = GetServerTime() + untilReset end

    local affixes = C_MythicPlus.GetCurrentAffixes()
    if affixes and #affixes > 0 then
        local ids = {}
        for i, affix in ipairs(affixes) do ids[i] = affix.id end
        db.affixes = ids
    end

    rewardLevels(db)

    local maps = C_ChallengeMode.GetMapTable()
    if maps and #maps > 0 then
        local list = {}
        for _, mapId in ipairs(maps) do
            local name, _, timeLimit = C_ChallengeMode.GetMapUIInfo(mapId)
            if name then list[mapId] = { name = name, timeLimit = timeLimit } end
        end
        db.maps = list
    end
end

local function bestRuns(db)
    local best = {}
    local summary = C_PlayerInfo and C_PlayerInfo.GetPlayerMythicPlusRatingSummary
        and C_PlayerInfo.GetPlayerMythicPlusRatingSummary("player")
    for _, run in ipairs(summary and summary.runs or {}) do
        local mapId, level = Plain(run.challengeModeID), Plain(run.bestRunLevel)
        if mapId and level and level > 0 then
            best[mapId] = {
                level = level,
                durationSec = math.floor((Plain(run.bestRunDurationMS) or 0) / 1000 + 0.5),
                overTime = not Plain(run.finishedSuccess),
                score = Plain(run.mapScore),
            }
        end
    end

    if C_MythicPlus.GetSeasonBestForMap then
        for mapId in pairs(db.maps) do
            if not best[mapId] then
                local inTime, overTime = C_MythicPlus.GetSeasonBestForMap(mapId)
                local run = inTime or overTime
                if run and Plain(run.level) then
                    best[mapId] = {
                        level = run.level,
                        durationSec = Plain(run.durationSec) or 0,
                        overTime = inTime == nil,
                        score = Plain(run.dungeonScore),
                    }
                end
            end
        end
    end

    for mapId in pairs(db.maps) do
        local scores, overall = C_MythicPlus.GetSeasonBestAffixScoreInfoForMap(mapId)
        if scores and not best[mapId] then
            local top
            for _, entry in ipairs(scores) do
                if not top or (Plain(entry.score) or 0) > (Plain(top.score) or 0) then top = entry end
            end
            if top then
                best[mapId] = {
                    level = top.level,
                    durationSec = top.durationSec,
                    overTime = top.overTime and true or false,
                    score = Plain(overall) or top.score,
                }
            end
        end
    end
    return best
end

local function rewardItemLevel(activity)
    if not (activity.id and (activity.level or 0) > 0 and C_WeeklyRewards.GetExampleRewardItemHyperlinks
        and C_Item.GetDetailedItemLevelInfo) then
        return nil
    end
    local link = C_WeeklyRewards.GetExampleRewardItemHyperlinks(activity.id)
    return link and Plain(C_Item.GetDetailedItemLevelInfo(link)) or nil
end

local function vault()
    local kind = Enum.WeeklyRewardChestThresholdType and Enum.WeeklyRewardChestThresholdType.Activities or 1
    local slots = {}
    for i, activity in ipairs(C_WeeklyRewards.GetActivities(kind) or {}) do
        slots[i] = {
            threshold = activity.threshold,
            progress = activity.progress,
            level = activity.level,
            itemLevel = rewardItemLevel(activity),
        }
    end
    return slots
end

local function weekRuns()
    local runs = {}
    for _, run in ipairs(C_MythicPlus.GetRunHistory(false, true) or {}) do
        local mapId, level = Plain(run.mapChallengeModeID), Plain(run.level)
        if mapId and level and level > 0 and run.thisWeek ~= false then
            runs[#runs + 1] = { mapId = mapId, level = level, timed = run.completed and true or false }
        end
    end
    return runs
end

function Snapshot:Take()
    local guid = Plain(UnitGUID("player"))
    local name = Plain(UnitName("player"))
    if not guid or not name then return end

    local db = T.DB:Data()
    seasonData(db)

    local _, classId = UnitClassBase("player")
    local char = db.chars[guid] or {}
    char.name = name
    char.realm = GetRealmName()
    char.id = T.Board.Me()
    if not IsInGuild() then
        char.guild = nil
    else
        local guildName, _, _, guildRealm = GetGuildInfo("player")
        guildName = Plain(guildName)
        if guildName then char.guild = guildName .. "-" .. (guildRealm or GetRealmName()) end
    end
    char.classId = classId
    char.level = UnitLevel("player")

    local mapId, level = self:OwnKey()
    char.key = mapId and { mapId = mapId, level = level } or nil

    local newSeason = char.season ~= db.season
    local rating = self:Rating()
    local best = next(db.maps) and bestRuns(db) or {}
    if consistent(rating, best) then
        retries = 0
        if rating > 0 or newSeason or not char.rating then char.rating = rating end
        if next(best) or newSeason or not char.best then char.best = best end
        char.season = db.season
    else
        if not char.rating then char.rating = rating end
        retryLater()
    end
    char.vault = vault()
    char.week = weekRuns()
    char.updatedAt = GetServerTime()

    db.chars[guid] = char
end
