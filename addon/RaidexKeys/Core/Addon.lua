local ADDON, T = ...
local L = T.L

local Addon = {}
T.Addon = Addon

local PREFIX = "|cffa78bffRaidex Keys|r: "

function Addon:Print(...)
    print(PREFIX .. strjoin(" ", tostringall(...)))
end

local issecretvalue = issecretvalue or function() return false end

function T.Plain(value)
    if value == nil or issecretvalue(value) then return nil end
    return value
end

local eventFrame = CreateFrame("Frame")
local handlers = {}

eventFrame:SetScript("OnEvent", function(_, event, ...)
    local list = handlers[event]
    if not list then return end
    for i = 1, #list do
        list[i](event, ...)
    end
end)

function Addon:On(event, handler)
    local list = handlers[event]
    if not list then
        if not pcall(eventFrame.RegisterEvent, eventFrame, event) then return end
        list = {}
        handlers[event] = list
    end
    list[#list + 1] = handler
end

local held = {}

Addon:On("MODIFIER_STATE_CHANGED", function(_, key, down)
    if type(key) ~= "string" then return end
    held[key] = down == 1
end)

local function anyHeld(left, right, api)
    if api and api() then return true end
    return held[left] == true or held[right] == true
end

function Addon:ShiftDown()
    return anyHeld("LSHIFT", "RSHIFT", IsShiftKeyDown)
end

function Addon:ControlDown()
    return anyHeld("LCTRL", "RCTRL", IsControlKeyDown)
end

local pending = {}
local pendingFn = {}
function Addon:Debounce(key, delay, fn)
    if pending[key] then pending[key]:Cancel() end
    pendingFn[key] = fn
    pending[key] = C_Timer.NewTimer(delay, function()
        pending[key] = nil
        pendingFn[key] = nil
        fn()
    end)
end

function Addon:Flush(key)
    local fn = pendingFn[key]
    if not fn then return end
    pending[key]:Cancel()
    pending[key], pendingFn[key] = nil, nil
    fn()
end

T.GRID_DEFAULT, T.GRID_MAX = 10, 40

function T.Snap(value, step)
    if not step or step <= 0 then return value end
    return math.floor(value / step + 0.5) * step
end

local dragDriver = CreateFrame("Frame")

function T.BeginDrag(frame)
    local step = T.DB:Settings().grid
    local left, top = frame:GetLeft(), frame:GetTop()
    if step <= 0 or not (left and top) then
        frame:StartMoving()
        return
    end
    local scale = frame:GetEffectiveScale()
    local x, y = GetCursorPosition()
    local grabX, grabY = x / scale - left, y / scale - top
    frame.gridDrag = true
    dragDriver:SetScript("OnUpdate", function()
        local cursorX, cursorY = GetCursorPosition()
        local per = frame:GetEffectiveScale() / UIParent:GetEffectiveScale()
        local newLeft = T.Snap((cursorX / scale - grabX) * per, step) / per
        local newTop = T.Snap((cursorY / scale - grabY) * per, step) / per
        frame:ClearAllPoints()
        frame:SetPoint("TOPLEFT", UIParent, "BOTTOMLEFT", newLeft, newTop)
    end)
end

function T.BeginSizing(frame, minW, minH, maxW, maxH)
    local settings = T.DB:Settings()
    local step = settings.grid
    if step <= 0 or settings.gridResize == false then
        frame:StartSizing("BOTTOMRIGHT")
        return
    end
    local scale = frame:GetEffectiveScale()
    local x, y = GetCursorPosition()
    local startX, startY = x / scale, y / scale
    local startW, startH = frame:GetWidth(), frame:GetHeight()
    frame.gridSizing = true
    dragDriver:SetScript("OnUpdate", function()
        local cursorX, cursorY = GetCursorPosition()
        local per = frame:GetEffectiveScale() / UIParent:GetEffectiveScale()
        local width = T.Snap((startW + cursorX / scale - startX) * per, step) / per
        local height = T.Snap((startH + startY - cursorY / scale) * per, step) / per
        frame:SetSize(math.max(minW, math.min(maxW, width)), math.max(minH, math.min(maxH, height)))
    end)
end

function T.EndSizing(frame)
    if frame.gridSizing then
        frame.gridSizing = nil
        dragDriver:SetScript("OnUpdate", nil)
    else
        frame:StopMovingOrSizing()
    end
end

function T.EndDrag(frame)
    if frame.gridDrag then
        frame.gridDrag = nil
        dragDriver:SetScript("OnUpdate", nil)
    else
        frame:StopMovingOrSizing()
    end
end

local HINT = "|cffc9a227"

function T.SayTimerHint()
    Addon:Print(L["Set the Mythic+ timer up before you run a key: dragging it into place and settling its look takes time a run does not give you."])
    Addon:Print(HINT .. L["Shift-click the minimap button to open the options before your next run. The timer stands there while they are open, to drag into place and to set."] .. "|r")
end

local function firstRun()
    local settings = T.DB:Settings()
    if settings.introSeen then return end
    settings.introSeen = true
    C_Timer.After(8, T.SayTimerHint)
end

BINDING_CATEGORY_RAIDEXKEYS = L["Raidex Keys"]
BINDING_NAME_RAIDEXKEYS_TIMER = L["Show or hide the Mythic+ timer"]
BINDING_NAME_RAIDEXKEYS_WINDOW = L["Open the Raidex Keys window"]

function RaidexKeys_ToggleTimer()
    T.Timer:SetShown(not T.Timer.Wanted(), true)
end

function RaidexKeys_ToggleWindow()
    T.Window:Toggle()
end

SLASH_RAIDEXKEYS1 = "/rk"
SLASH_RAIDEXKEYS2 = "/raidexkeys"
SlashCmdList.RAIDEXKEYS = function(input)
    local command = strtrim((input or ""):lower())
    if command == "options" or command == "config" then
        T.OpenOptions()
    elseif command == "window" or command == "keys" then
        T.Window:Toggle()
    elseif command == "preview" or command == "demo" then
        T.Preview:Toggle()
    elseif command == "minimap" then
        T.MinimapButton:SetShown(T.DB:Settings().minimap.hide)
    elseif command == "timer" then
        T.Timer:TogglePreview()
    elseif command == "reset" then
        T.ResetToDefaults()
        Addon:Print(L["The timer looks as designed again."])
        Addon:Print(L["Window, timer and minimap button stand where they first stood."])
    elseif command == "debug" then
        local ok, err = pcall(Addon.PrintDebug, Addon)
        if not ok then Addon:Print("debug failed: " .. tostring(err)) end
    else
        Addon:PrintStatus()
    end
end

function Addon:PrintStatus()
    local db = T.DB
    self:Print(L["Characters stored: %d"]:format(db:CountCharacters()))
    self:Print(L["Guild keys this week: %d"]:format(T.Exchange:GuildKeysThisWeek()))
    self:Print(T.Exchange:UsesLibKeystone()
        and L["Keystone exchange: through LibKeystone (another addon)"]
        or L["Keystone exchange: built in"])
    self:Print(L["Reward levels known: %d"]:format(db:CountRewardLevels()))
    self:Print(L["Data is saved when you log out or type /reload."])
    self:Print(L["Commands: /rk window, /rk preview, /rk options, /rk minimap, /rk timer, /rk reset, /rk debug"])
end

function Addon:PrintDebug()
    local function show(label, ...)
        local parts = {}
        for i = 1, select("#", ...) do parts[i] = tostring(T.Plain((select(i, ...))) or "nil/secret") end
        self:Print("debug " .. label .. ": " .. table.concat(parts, ", "))
    end
    show("addon version, build", C_AddOns.GetAddOnMetadata("RaidexKeys", "Version"),
        C_AddOns.GetAddOnMetadata("RaidexKeys", "X-Build"))
    show("options Defaults followed", T.DefaultsHooked and T.DefaultsHooked() and "yes" or "no")
    show("season", C_MythicPlus.GetCurrentSeason())
    show("overall score", C_ChallengeMode.GetOverallDungeonScore())
    show("owned key (map, level)", C_MythicPlus.GetOwnedKeystoneChallengeMapID(), C_MythicPlus.GetOwnedKeystoneLevel())

    local summary = C_PlayerInfo and C_PlayerInfo.GetPlayerMythicPlusRatingSummary
        and C_PlayerInfo.GetPlayerMythicPlusRatingSummary("player")
    show("rating summary (score, runs)", summary and summary.currentSeasonScore, summary and summary.runs and #summary.runs)
    local first = summary and summary.runs and summary.runs[1]
    if first then
        local fields = {}
        for key, value in pairs(first) do fields[#fields + 1] = key .. "=" .. tostring(T.Plain(value)) end
        table.sort(fields)
        self:Print("debug summary run[1]: " .. table.concat(fields, " "))
    end

    local rewards, known = T.Snapshot.RewardLevels(), 0
    for _ in pairs(rewards) do known = known + 1 end
    show("reward levels the game answers (of 29)", known)
    if C_MythicPlus.GetRewardLevelForDifficultyLevel then
        show("+10 reward (weekly, end of run)", C_MythicPlus.GetRewardLevelForDifficultyLevel(10))
    end
    if C_MythicPlus.GetRewardLevelFromKeystoneLevel then
        show("+10 reward (from keystone level)", C_MythicPlus.GetRewardLevelFromKeystoneLevel(10))
    end
    local stored = T.DB:Data().rewards
    if stored and stored[10] then
        show("+10 stored (item level, vault)", stored[10].itemLevel, stored[10].vaultItemLevel)
    end

    local maps = C_ChallengeMode.GetMapTable() or {}
    show("season maps", #maps)
    local mapId = maps[1]
    if mapId then
        local scores, overall = C_MythicPlus.GetSeasonBestAffixScoreInfoForMap(mapId)
        show("affix scores for " .. mapId .. " (entries, overall)", scores and #scores, overall)
        if C_MythicPlus.GetSeasonBestForMap then
            local inTime, overTime = C_MythicPlus.GetSeasonBestForMap(mapId)
            show("season best for " .. mapId .. " (in time, over time)",
                inTime and (inTime.level .. " in " .. inTime.durationSec .. "s"),
                overTime and (overTime.level .. " in " .. overTime.durationSec .. "s"))
        end
    end
    local history = C_MythicPlus.GetRunHistory and C_MythicPlus.GetRunHistory(true, true)
    show("run history (all weeks)", history and #history)

    show("guild raids in the calendar (count, first key)", T.Raids:DebugInfo())
    T.Raids:View()
    T.Raids.ReadProgress()
    for _, raid in ipairs(T.Raids:View() or {}) do
        show("raid " .. raid.key, ("%s, map %s, difficulty %s, %s, progress %s"):format(tostring(raid.instanceName),
            tostring(raid.instanceMap), tostring(raid.difficultyId), T.Raids.DebugBosses(raid.instanceName, raid.instanceMap),
            raid.bosses and (raid.killed .. "/" .. raid.bosses) or "none"))
        show("criteria " .. raid.key, T.Raids.DebugCriteria(raid.instanceName, raid.instanceMap,
            raid.difficultyName, raid.difficultyId))
    end

    local guid = T.Plain(UnitGUID("player"))
    local char = guid and T.DB:Data().chars[guid]
    local count = 0
    for _ in pairs(char and char.best or {}) do count = count + 1 end
    local rating = char and char.rating or 0
    self:Print(("debug check rating > 0 needs runs: stored rating %s, stored runs %d -> %s"):format(
        tostring(rating), count, (rating == 0 or count > 0) and "OK" or "NOT MET (game data not loaded yet)"))
end

local loader = CreateFrame("Frame")
loader:RegisterEvent("ADDON_LOADED")
loader:RegisterEvent("PLAYER_LOGIN")
loader:SetScript("OnEvent", function(self, event, name)
    if event == "ADDON_LOADED" and name == ADDON then
        T.DB:Initialize()
        T.Themes.Apply()
        T.SetupOptions()
    elseif event == "PLAYER_LOGIN" then
        self:UnregisterAllEvents()
        T.Snapshot:Start()
        T.Exchange:Start()
        T.Board:Start()
        T.Raids:Start()
        T.Inbox:Start()
        T.MinimapButton:Start()
        T.Timer:Start()
        T.Window:Start()
        T.WatchOptionsPanel()
        firstRun()
    end
end)
