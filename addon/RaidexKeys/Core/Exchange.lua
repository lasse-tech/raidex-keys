local _, T = ...
local Addon, Plain = T.Addon, T.Plain

local PREFIX = "LibKS"
local THROTTLE = 3
local GUILD_INTERVAL = 600
local ENOUGH_TIME = 10
local GUILD_REFRESH = 60

local Exchange = {}
T.Exchange = Exchange

local lks
local party = {}

local function fullName(name)
    if not name then return nil end
    if name:find("-", 1, true) then return name end
    return name .. "-" .. (GetNormalizedRealmName() or "")
end

local function isSelf(name)
    return fullName(name) == fullName(UnitName("player"))
end

local running = {}

function Exchange:HeardFrom(who)
    if who then running[who] = true end
end

function Exchange:RunsAddon(who)
    if not who then return false end
    return running[who] == true or isSelf(who)
end

local function currentGuild()
    if not IsInGuild() then return nil end
    local name, _, _, realm = GetGuildInfo("player")
    name = Plain(name)
    if not name then return nil end
    return T.DB:Guild(name, realm or GetRealmName())
end

local function guildKey()
    local guild = currentGuild()
    return guild and (guild.name .. "-" .. guild.realm)
end

local function rosterClasses()
    local key = guildKey()
    return key and T.DB:Data().roster[key] or {}
end

local ROSTER_APART = 15
local rosterAt

local function readRoster()
    if not (IsInGuild() and GetGuildRosterInfo and GetNumGuildMembers) then return end
    local since = rosterAt and GetTime() - rosterAt
    if since and since < ROSTER_APART then
        Addon:Debounce("roster", ROSTER_APART - since, readRoster)
        return
    end
    rosterAt = GetTime()
    local classes = {}
    for i = 1, (GetNumGuildMembers() or 0) do
        local name, _, _, _, _, _, _, _, _, _, classFile = GetGuildRosterInfo(i)
        name, classFile = Plain(name), Plain(classFile)
        if name and classFile then classes[fullName(name)] = classFile end
    end
    local key = guildKey()
    if not (key and next(classes)) then return end
    T.DB:Data().roster[key] = classes
    if T.Window then T.Window:Refresh() end
end

local function requestRoster()
    if C_GuildInfo and C_GuildInfo.GuildRoster then C_GuildInfo.GuildRoster() end
end

local function received(level, mapId, rating, sender, channel)
    local who = fullName(sender)
    if not (who and level) then return end
    if level < 0 or isSelf(who) then return end
    local entry = {
        mapId = level > 0 and mapId or nil,
        level = level > 0 and level or nil,
        rating = rating,
        seenAt = GetServerTime(),
    }
    if channel == "GUILD" then
        local guild = currentGuild()
        if not guild then return end
        guild.keys[who] = entry
    elseif channel == "PARTY" then
        party[who] = entry
    end
end

local lastSent = {}
local waiting = {}

local function canSend(channel)
    if IsInInstance() then return false end
    if channel == "GUILD" then return IsInGuild() end
    return IsInGroup(LE_PARTY_CATEGORY_HOME)
end

local function send(channel, kind, build)
    if not canSend(channel) then return end
    local slot = channel .. " " .. kind
    local wait = THROTTLE - (GetTime() - (lastSent[slot] or 0))
    if wait > 0 then
        if not waiting[slot] then
            waiting[slot] = C_Timer.NewTimer(wait + 0.1, function()
                waiting[slot] = nil
                send(channel, kind, build)
            end)
        end
        return
    end
    lastSent[slot] = GetTime()
    C_ChatInfo.SendAddonMessage(PREFIX, build(), channel)
end

local function ownAnswer(channel)
    return function()
        local mapId, level = T.Snapshot:OwnKey()
        local rating = math.floor(T.Snapshot:Rating() + 0.5)
        if not mapId then
            return ("0,0,%d"):format(rating)
        end
        if channel == "GUILD" and not T.DB:Settings().shareWithGuild then
            return ("-1,-1,%d"):format(rating)
        end
        return ("%d,%d,%d"):format(level, mapId, rating)
    end
end

local function onAddonMessage(_, prefix, message, channel, sender)
    if prefix ~= PREFIX or (channel ~= "PARTY" and channel ~= "GUILD") then return end
    message, sender = Plain(message), Plain(sender)
    if not message or not sender then return end
    if message == "R" then
        if not isSelf(sender) then send(channel, "answer", ownAnswer(channel)) end
        return
    end
    local level, mapId, rating = message:match("^(%-?%d+),(%-?%d+),(%d+)$")
    if level then
        received(tonumber(level), tonumber(mapId), tonumber(rating), sender, channel)
    end
end

local lastAsked = { PARTY = 0, GUILD = 0 }

function Exchange:Ask(channel)
    if not canSend(channel) then return end
    lastAsked[channel] = GetTime()
    if lks and lks.Request then
        lks.Request(channel)
    else
        send(channel, "R", function() return "R" end)
    end
end

function Exchange:RefreshParty()
    if GetTime() - lastAsked.PARTY > ENOUGH_TIME then self:Ask("PARTY") end
end

function Exchange:RefreshGuild()
    requestRoster()
    if GetTime() - lastAsked.GUILD > GUILD_REFRESH then self:Ask("GUILD") end
end

function Exchange:UsesLibKeystone()
    return lks ~= nil
end

local function memberKey(who)
    if party[who] then return party[who] end
    local guild = currentGuild()
    local entry = guild and guild.keys[who]
    if entry and entry.level and (entry.seenAt or 0) >= T.DB:WeekStart() then return entry end
    return nil
end

function Exchange:PartyKnown()
    local size = GetNumGroupMembers()
    if size == 0 then return nil end
    local known = 1
    local prefix = IsInRaid() and "raid" or "party"
    for i = 1, size do
        local name, realm = UnitFullName(prefix .. i)
        name, realm = Plain(name), Plain(realm)
        if name then
            local who = realm and realm ~= "" and (name .. "-" .. realm) or fullName(name)
            if not isSelf(who) and memberKey(who) then known = known + 1 end
        end
    end
    return known, size
end

function Exchange:PartyKeys()
    local size = GetNumGroupMembers()
    if size == 0 then return {} end

    local mapId, level = T.Snapshot:OwnKey()
    local _, classId = UnitClassBase("player")
    local list = { { name = Plain(UnitName("player")), who = fullName(Plain(UnitName("player"))),
        mapId = mapId, level = level,
        rating = math.floor(T.Snapshot:Rating() + 0.5), classId = classId,
        seenAt = GetServerTime() } }

    local prefix = IsInRaid() and "raid" or "party"
    for i = 1, size do
        local unit = prefix .. i
        local name, realm = UnitFullName(unit)
        name, realm = Plain(name), Plain(realm)
        if name then
            local who = realm and realm ~= "" and (name .. "-" .. realm) or fullName(name)
            if not isSelf(who) then
                local entry = memberKey(who) or {}
                local _, memberClass = UnitClassBase(unit)
                list[#list + 1] = { name = name, who = who, mapId = entry.mapId, level = entry.level,
                    rating = entry.rating, classId = Plain(memberClass), seenAt = entry.seenAt }
            end
        end
    end
    return list
end

local function shortName(who)
    local name, realm = who:match("^(.-)%-(.+)$")
    if not name then return who end
    if realm == (GetNormalizedRealmName() or GetRealmName()) then return name end
    return who
end

Exchange.ShortName = shortName

function Exchange:ClassOf(who)
    return rosterClasses()[who]
end

local function ownGuildKey()
    local mapId, level = T.Snapshot:OwnKey()
    if not level then return nil end
    local _, classId = UnitClassBase("player")
    return {
        name = Plain(UnitName("player")), who = fullName(Plain(UnitName("player"))),
        mapId = mapId, level = level,
        rating = math.floor(T.Snapshot:Rating() + 0.5), classId = Plain(classId),
        seenAt = GetServerTime(),
    }
end

function Exchange:GuildView()
    local guild = currentGuild()
    if not guild then return nil end
    local since, keys = T.DB:WeekStart(), {}
    local own = ownGuildKey()
    if own then keys[1] = own end
    local classes = rosterClasses()
    for who, entry in pairs(guild.keys) do
        if entry.level and (entry.seenAt or 0) >= since then
            keys[#keys + 1] = { name = shortName(who), who = who, mapId = entry.mapId, level = entry.level,
                rating = entry.rating, classFile = classes[who], seenAt = entry.seenAt }
        end
    end
    return { name = guild.name, keys = keys }
end

function Exchange:GuildKeysThisWeek()
    local guild = currentGuild()
    if not guild then return 0 end
    local since, n = T.DB:WeekStart(), 0
    for _, entry in pairs(guild.keys) do
        if entry.level and (entry.seenAt or 0) >= since then n = n + 1 end
    end
    if ownGuildKey() then n = n + 1 end
    return n
end

local function prune()
    local since = T.DB:WeekStart()
    for _, guild in pairs(T.DB:Data().guilds) do
        for who, entry in pairs(guild.keys) do
            if (entry.seenAt or 0) < since then guild.keys[who] = nil end
        end
    end
end

function Exchange:Start()
    prune()
    lks = LibStub and LibStub("LibKeystone", true)
    if lks then
        lks.Register(Exchange, function(level, mapId, rating, sender, channel)
            received(Plain(level), Plain(mapId), Plain(rating), Plain(sender), channel)
        end)
        if not T.DB:Settings().shareWithGuild and lks.SetGuildHidden then
            lks.SetGuildHidden(true)
        end
    else
        C_ChatInfo.RegisterAddonMessagePrefix(PREFIX)
        Addon:On("CHAT_MSG_ADDON", onAddonMessage)
    end

    Addon:On("GROUP_ROSTER_UPDATE", function()
        Addon:Debounce("party", 2, function() Exchange:Ask("PARTY") end)
    end)
    Addon:On("GROUP_LEFT", function() wipe(party) end)
    Addon:On("GUILD_ROSTER_UPDATE", function()
        Addon:Debounce("roster", 2, readRoster)
    end)

    requestRoster()
    C_Timer.After(15, function()
        Exchange:Ask("GUILD")
        Exchange:Ask("PARTY")
    end)
    C_Timer.NewTicker(GUILD_INTERVAL, function() Exchange:Ask("GUILD") end)
end

function Exchange:ShareChanged(share)
    if lks and lks.SetGuildHidden then lks.SetGuildHidden(not share) end
end
