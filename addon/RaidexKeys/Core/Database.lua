local _, T = ...

local SCHEMA = 1

local defaults = {
    chars = {},
    guilds = {},
    maps = {},
    affixes = {},
    timer = { best = {} },
    roster = {},
    board = {},
    raids = {},
    inbox = {},
    settings = {
        shareWithGuild = true,
        theme = "slate",
        introSeen = false,
        grid = T.GRID_DEFAULT,
        gridResize = true,
        minimap = { hide = false, angle = 225 },
        timer = {
            enabled = true, hideBlizzard = true, hideQuests = false,
            announce = true, bossTimes = true,
            alpha = 0.8, font = "frizqt", fontSize = 11, spacing = 0.75,
            showAffixes = false, showScore = true, maxBosses = 6, scale = 0.9,
            background = { 0.094, 0.110, 0.137 },
            text = { 0.941, 0.953, 0.973 },
        },
        window = { font = "marcellus", fontSize = 12, timeFormat = "auto", dateFormat = "auto" },
    },
}

local function fill(target, source)
    for key, value in pairs(source) do
        if type(value) == "table" then
            if type(target[key]) ~= "table" then target[key] = {} end
            fill(target[key], value)
        elseif target[key] == nil then
            target[key] = value
        end
    end
end

local DB = {}
T.DB = DB

function DB:Initialize()
    if type(RaidexKeysDB) ~= "table" then RaidexKeysDB = {} end
    local db = RaidexKeysDB
    fill(db, defaults)
    db.schema = SCHEMA
    self.db = db
end

function DB:Data() return self.db end

function DB:CountRewardLevels()
    local n = 0
    for _ in pairs(self.db.rewards or {}) do n = n + 1 end
    return n
end
function DB:Settings() return self.db.settings end

function DB:CountCharacters()
    local n = 0
    for _ in pairs(self.db.chars) do n = n + 1 end
    return n
end

function DB:Guild(name, realm)
    local key = name .. "-" .. realm
    local guild = self.db.guilds[key]
    if not guild then
        guild = { name = name, realm = realm, keys = {} }
        self.db.guilds[key] = guild
    end
    return guild
end

function DB:WeekStart()
    local untilReset = C_DateAndTime.GetSecondsUntilWeeklyReset() or 0
    return GetServerTime() + untilReset - 7 * 24 * 60 * 60
end
