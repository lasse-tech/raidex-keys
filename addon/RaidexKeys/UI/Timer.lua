local _, T = ...
local L, Plain = T.L, T.Plain

local Timer = {}
T.Timer = Timer

local DEFAULT_ALPHA = 0.8
local DEFAULT_SIZE = 11
local DEFAULT_FONT = "frizqt"
local DEFAULT_AFFIXES, DEFAULT_SCORE, DEFAULT_BOSSES = false, true, 6
local DEFAULT_SCALE = 0.9
local DEFAULT_SPACING = 0.75
Timer.DEFAULT_ALPHA, Timer.DEFAULT_SIZE, Timer.DEFAULT_FONT = DEFAULT_ALPHA, DEFAULT_SIZE, DEFAULT_FONT
Timer.DEFAULT_AFFIXES, Timer.DEFAULT_SCORE, Timer.DEFAULT_BOSSES = DEFAULT_AFFIXES, DEFAULT_SCORE, DEFAULT_BOSSES
Timer.DEFAULT_SCALE, Timer.DEFAULT_SPACING = DEFAULT_SCALE, DEFAULT_SPACING

local RAMP = {
    text  = { 0.910, 0.918, 0.965 },
    body  = { 0.686, 0.714, 0.839 },
    muted = { 0.557, 0.580, 0.722 },
    dim   = { 0.431, 0.459, 0.600 },
    faint = { 0.306, 0.329, 0.447 },
    split = { 0.357, 0.380, 0.533 },
    mark  = { 0.227, 0.251, 0.376 },
}

local MEANING = {
    keystone = { 0.639, 0.208, 0.933 },
    gold     = { 0.788, 0.635, 0.153 },
    red      = { 1.000, 0.302, 0.302 },
    glow     = { 0.655, 0.545, 1.000 },
}

local CARD = {
    rule      = { 0.106, 0.125, 0.220 },
    track     = { 0.086, 0.106, 0.188 },
    trackEdge = { 0.137, 0.161, 0.263 },
}

local FIXED = {
    edge      = { 0.486, 0.361, 1.000, 0.22 },
    accent    = { 0.486, 0.361, 1.000, 0.35 },
    goldLow   = { 0.604, 0.482, 0.118, 1 },
    goldHigh  = { 0.878, 0.718, 0.200, 1 },
    glowLow   = { 0.357, 0.282, 0.722, 1 },
    glowHigh  = { 0.655, 0.545, 1.000, 1 },
    markLow   = { 0.357, 0.380, 0.533, 1 },
    markHigh  = { 0.557, 0.580, 0.722, 1 },
}

local P, C, SIZES = {}, {}, {}
local SPACING = DEFAULT_SPACING
local SHOW_AFFIXES, SHOW_SCORE = DEFAULT_AFFIXES, DEFAULT_SCORE
local MAX_BOSSES = DEFAULT_BOSSES
local SCALE = DEFAULT_SCALE

local CHESTS = { 0.8, 0.6 }

local CHALLENGE_TIMER = LE_WORLD_ELAPSED_TIMER_TYPE_CHALLENGE_MODE or 1
local TICK = 0.5

local PADX, PADY = 16, 14
local BODY = 14
local PROPORTIONS = { body = 1, title = 17 / BODY, clock = 20 / BODY, small = 12 / BODY }
local MARKER_W, MARKER_GAP = 14, 10
local COLUMN_GAP = 24
local MIN_WIDTH = 300
local BAR_H, FORCES_BAR_H = 7, 5
local LEADING = 2
local SEPARATOR = "\226\128\162"

local MARKER_SIZE = 7

Timer.SIZE_MIN, Timer.SIZE_MAX = 10, 22
Timer.ALPHA_MIN, Timer.ALPHA_MAX = 0.2, 1
Timer.SPACING_MIN, Timer.SPACING_MAX = 0.6, 1.5
Timer.BOSSES_MIN, Timer.BOSSES_MAX = 0, 6
Timer.SCALE_MIN, Timer.SCALE_MAX = 0.5, 2

local function setFont(region, size, display)
    T.Fonts.Set(region, T.DB:Settings().timer.font, size, "OUTLINE",
        display, DEFAULT_FONT)
end

local function clamp(value, low, high)
    return math.max(low or 0, math.min(high or 1, value or 0))
end

local function hex(color)
    return ("|cff%02x%02x%02x"):format(
        math.floor(clamp(color[1]) * 255 + 0.5),
        math.floor(clamp(color[2]) * 255 + 0.5),
        math.floor(clamp(color[3]) * 255 + 0.5))
end

local function shift(color, by, alpha)
    return { clamp(color[1] + by[1]), clamp(color[2] + by[2]), clamp(color[3] + by[3]), alpha }
end

local function difference(color, from)
    return { (color[1] or 0) - from[1], (color[2] or 0) - from[2], (color[3] or 0) - from[3] }
end

function Timer.Style()
    local settings = T.DB:Settings().timer
    local baseBack, baseText = T.Themes.TimerDefaults()
    local text = settings.text or baseText
    local back = settings.background or baseBack
    local themed = T.Themes.Current() ~= "raidex"
    local colors, mix = T.Themes.Colors, T.Themes.Mix
    local ramp = themed and { text = colors.text, body = colors.body,
        muted = colors.muted, dim = colors.faint,
        split = mix(colors.muted, colors.bg, 0.4),
        faint = mix(colors.muted, colors.bg, 0.55),
        mark = mix(colors.muted, colors.bg, 0.7) } or RAMP
    local card = themed and { rule = colors.divider, track = colors.header,
        trackEdge = colors.tab } or CARD
    local textBy, backBy = difference(text, baseText), difference(back, baseBack)

    for name, color in pairs(ramp) do P[name] = hex(shift(color, textBy)) end
    for name, color in pairs(MEANING) do P[name] = hex(color) end
    if themed then
        P.gold, P.glow = hex(colors.gold), hex(colors.glow)
    end

    C.card = { clamp(back[1]), clamp(back[2]), clamp(back[3]),
        clamp(settings.alpha or DEFAULT_ALPHA, Timer.ALPHA_MIN, Timer.ALPHA_MAX) }
    for name, color in pairs(card) do C[name] = shift(color, backBy, 1) end
    for name, color in pairs(FIXED) do C[name] = color end
    if themed then
        local function rgba(color, alpha, scale)
            return { color[1] * (scale or 1), color[2] * (scale or 1),
                color[3] * (scale or 1), alpha or 1 }
        end
        C.edge, C.accent = rgba(colors.glow, 0.22), rgba(colors.glow, 0.35)
        C.goldLow, C.goldHigh = rgba(colors.gold, 1, 0.7), rgba(colors.gold)
        C.glowLow, C.glowHigh = rgba(colors.glow, 1, 0.7), rgba(colors.glow)
        C.markLow, C.markHigh = rgba(ramp.split), rgba(colors.muted)
    end

    C.markerDone = shift(ramp.dim, textBy, 1)
    C.markerOpen = shift(ramp.mark, textBy, 1)

    local size = clamp(settings.fontSize or DEFAULT_SIZE, Timer.SIZE_MIN, Timer.SIZE_MAX)
    for name, share in pairs(PROPORTIONS) do SIZES[name] = math.floor(size * share + 0.5) end

    SPACING = clamp(settings.spacing or DEFAULT_SPACING, Timer.SPACING_MIN, Timer.SPACING_MAX)
    SHOW_AFFIXES = settings.showAffixes ~= false
    SHOW_SCORE = settings.showScore ~= false
    MAX_BOSSES = clamp(settings.maxBosses or DEFAULT_BOSSES, Timer.BOSSES_MIN, Timer.BOSSES_MAX)
    SCALE = clamp(settings.scale or DEFAULT_SCALE, Timer.SCALE_MIN, Timer.SCALE_MAX)
end

function Timer.Palette() return C, P end

local DECIMAL = GetLocale() == "deDE" and "," or "."

Timer.Score = T.Rating.Score

function Timer.Clock(seconds)
    local sign = seconds < 0 and "-" or ""
    seconds = math.floor(math.abs(seconds))
    return ("%s%02d:%02d"):format(sign, math.floor(seconds / 60), seconds % 60)
end

function Timer.Percent(value)
    return (("%.2f"):format(value):gsub("%.", DECIMAL))
end

local function visibleCriteria(criteria, limit)
    local bosses = 0
    for _, c in ipairs(criteria) do
        if not c.weighted then bosses = bosses + 1 end
    end
    if limit >= bosses then return criteria end

    local standing, seen = bosses, 0
    for _, c in ipairs(criteria) do
        if not c.weighted then
            seen = seen + 1
            if not c.done then
                standing = seen
                break
            end
        end
    end
    local first = math.max(1, math.min(standing, bosses - limit + 1))

    local shown
    shown, seen = {}, 0
    for _, c in ipairs(criteria) do
        if c.weighted then
            shown[#shown + 1] = c
        else
            seen = seen + 1
            if seen >= first and seen < first + limit then shown[#shown + 1] = c end
        end
    end
    return shown
end

function Timer.Lines(state)
    local lines = {}
    local function add(row) lines[#lines + 1] = row return row end

    add({ kind = "text", size = "title", display = true,
        left = ("%s+%d|r %s%s|r"):format(P.keystone, state.level, P.text, state.name or "?") })
    local affixes = SHOW_AFFIXES and state.affixes or {}
    if #affixes > 0 then
        add({ kind = "text", size = "small", gap = 5, wrap = true,
            left = P.muted .. table.concat(affixes, "|r " .. P.mark .. SEPARATOR .. "|r " .. P.muted) .. "|r" })
    end
    add({ kind = "rule", accent = true, gap = 12 })

    local elapsed, limit = state.elapsed or 0, state.limit
    if limit and limit > 0 then
        local remaining = limit - elapsed
        add({ kind = "text", size = "clock", display = true, gap = 14,
            left = (remaining >= 0 and P.gold or P.red) .. Timer.Clock(remaining) .. "|r",
            label = P.dim .. L["LEFT"] .. "|r",
            right = ("%s%s|r %s/ %s|r"):format(P.body, Timer.Clock(elapsed), P.split, Timer.Clock(limit)) })
        add({ kind = "bar", gap = 11, tone = "gold", height = BAR_H,
            fill = elapsed / limit, marks = { CHESTS[2], CHESTS[1] } })

        local parts = {}
        for i, share in ipairs(CHESTS) do
            local cut = math.floor(limit * share)
            if elapsed < cut then
                parts[#parts + 1] = ("%s+%d|r %s(%s)|r %s%s|r"):format(
                    P.text, i + 1, P.dim, Timer.Clock(cut), P.gold, L["in %s"]:format(Timer.Clock(cut - elapsed)))
            else
                parts[#parts + 1] = ("%s+%d|r %s(%s)|r %s%s|r"):format(
                    P.dim, i + 1, P.faint, Timer.Clock(cut), P.dim, L["missed"])
            end
        end
        add({ kind = "text", size = "small", gap = 7, left = table.concat(parts, "   ") })
    else
        add({ kind = "text", size = "clock", display = true, gap = 14,
            left = P.gold .. Timer.Clock(elapsed) .. "|r" })
    end

    local criteria = visibleCriteria(state.criteria or {}, MAX_BOSSES)
    if #criteria > 0 then add({ kind = "rule", gap = 16 }) end
    for i, c in ipairs(criteria) do
        local gap = i == 1 and 13 or 8
        local marker = c.done and "done" or "open"
        if c.weighted then
            add({ kind = "text", gap = gap, marker = marker,
                left = P.text .. L["Enemy Forces"] .. "|r",
                right = P.text .. L["%s%%"]:format(Timer.Percent(c.percent or 0)) .. "|r" })
            add({ kind = "bar", gap = 8, tone = "glow", height = FORCES_BAR_H,
                fill = (c.percent or 0) / 100 })
        elseif c.done then
            local value = c.split and (P.muted .. Timer.Clock(c.split) .. "|r") or ""
            if c.best then
                local best = P.faint .. L["best %s"]:format(Timer.Clock(c.best)) .. "|r"
                value = c.split and (value .. " " .. P.faint .. "·|r " .. best) or best
            end
            add({ kind = "text", gap = gap, marker = marker,
                left = P.muted .. c.name .. "|r", right = value ~= "" and value or nil })
        else
            local open = (c.total or 1) > 1
                and ("%s%d / %d|r"):format(P.dim, c.quantity or 0, c.total)
                or nil
            add({ kind = "text", gap = gap, marker = marker,
                left = P.text .. c.name .. "|r", right = open })
        end
    end

    add({ kind = "rule", gap = 13 })
    local foot = { kind = "text", gap = 12 }
    if limit and limit > 0 and SHOW_SCORE then
        local rating, gain = Timer.Projected(state, elapsed, limit)
        foot.left = ("%s%s|r %s%d|r %s%s|r"):format(P.muted, L["Finish now"], P.glow,
            rating, P.dim, L["(+%d rating)"]:format(gain))
    end
    local deaths = state.deaths or 0
    if deaths > 0 then
        local text = deaths == 1 and L["1 Death"] or L["%d Deaths"]:format(deaths)
        foot.right = P.muted .. text .. "|r"
        if (state.timeLost or 0) > 0 then
            foot.right = ("%s %s−%s|r"):format(foot.right, P.red, Timer.Clock(state.timeLost))
        end
    end
    if foot.left or foot.right then add(foot) else lines[#lines] = nil end
    return lines
end

local EXAMPLE_LEVEL, EXAMPLE_LIMIT = 12, 1800
local EXAMPLE_PACE = 1143 / 1800
local SAMPLE_LEVEL = 8
Timer.DEFAULT_PREVIEW_LEVEL = EXAMPLE_LEVEL

local function mapInfo(mapId)
    if C_ChallengeMode and C_ChallengeMode.GetMapUIInfo then
        local name, _, timeLimit = C_ChallengeMode.GetMapUIInfo(mapId)
        if name then return name, Plain(timeLimit) end
    end
    local stored = (T.DB:Data().maps or {})[mapId]
    if stored then return stored.name, stored.timeLimit end
    return nil
end

local function sampleMapId()
    local ids = {}
    local maps = C_ChallengeMode and C_ChallengeMode.GetMapTable and C_ChallengeMode.GetMapTable()
    for _, mapId in ipairs(maps or {}) do ids[#ids + 1] = mapId end
    if #ids == 0 then
        for mapId in pairs(T.DB:Data().maps or {}) do ids[#ids + 1] = mapId end
        table.sort(ids)
    end
    if #ids == 0 then return nil end
    return ids[math.random(#ids)]
end

local drawn

local function choice()
    local timer = T.DB:Settings().timer
    return { map = timer.previewKey or "own", level = timer.previewLevel or EXAMPLE_LEVEL }
end
local LETTERS = "ÄÖÜ äöü ß · – éè"

function Timer.PreviewChoice()
    return choice()
end

function Timer:SetPreviewChoice(map, level)
    local timer = T.DB:Settings().timer
    if map ~= nil then timer.previewKey = map end
    if level ~= nil then timer.previewLevel = level end
    self:Update()
end

function Timer.SeasonDungeons()
    local ids = {}
    local maps = C_ChallengeMode and C_ChallengeMode.GetMapTable and C_ChallengeMode.GetMapTable()
    for _, mapId in ipairs(maps or {}) do ids[#ids + 1] = mapId end
    if #ids == 0 then
        for mapId in pairs(T.DB:Data().maps or {}) do ids[#ids + 1] = mapId end
    end
    local list = {}
    for _, mapId in ipairs(ids) do
        local name = mapInfo(mapId)
        if name then list[#list + 1] = { mapId = mapId, name = name } end
    end
    table.sort(list, function(a, b) return a.name < b.name end)
    return list
end

local function previewKey()
    local chosen = choice()
    if chosen.map == "letters" then return chosen.level, LETTERS, EXAMPLE_LIMIT end
    local picked = tonumber(chosen.map)
    if picked then
        local name, limit = mapInfo(picked)
        if name then return chosen.level, name, limit end
    end
    local mapId, level = T.Snapshot:OwnKey()
    if not mapId then
        if drawn == nil then drawn = sampleMapId() or false end
        if drawn then mapId, level = drawn, SAMPLE_LEVEL end
    end
    if not mapId then return nil end
    local name, limit = mapInfo(mapId)
    if not name then return nil end
    return level, name, limit
end

local journal
local bosses = {}

local function readJournal()
    if journal then return journal end
    journal = {}
    if not (EJ_GetNumTiers and EJ_SelectTier and EJ_GetCurrentTier and EJ_GetInstanceByIndex) then
        return journal
    end
    local theirs = EJ_GetCurrentTier()
    for tier = 1, Plain(EJ_GetNumTiers()) or 0 do
        EJ_SelectTier(tier)
        local i = 1
        while true do
            local instanceId, name = EJ_GetInstanceByIndex(i, false)
            if not instanceId then break end
            if name and not journal[name] then journal[name] = instanceId end
            i = i + 1
        end
    end
    if theirs then EJ_SelectTier(theirs) end
    return journal
end

local function bossesOf(name)
    if bosses[name] ~= nil then return bosses[name] or nil end
    bosses[name] = false
    local instanceId = readJournal()[name]
    if not (instanceId and EJ_SelectInstance and EJ_GetEncounterInfoByIndex) then return nil end

    local theirs = EncounterJournal and EncounterJournal.instanceID
    EJ_SelectInstance(instanceId)
    local list, i = {}, 1
    while true do
        local boss = EJ_GetEncounterInfoByIndex(i)
        if not boss then break end
        list[#list + 1] = boss
        i = i + 1
    end
    if type(theirs) == "number" then EJ_SelectInstance(theirs) end

    if #list > 0 then bosses[name] = list end
    return bosses[name] or nil
end

local function previewCriteria(dungeon)
    local list = dungeon and bossesOf(dungeon)
    if not list then
        return {
            { name = L["Example boss defeated"], done = true, split = 402, best = 395 },
            { name = L["Second example boss defeated"], quantity = 0, total = 1 },
            { weighted = true, percent = 64.3 },
        }
    end
    local criteria = {}
    local felled = math.floor(#list * EXAMPLE_PACE)
    for i, boss in ipairs(list) do
        if i <= felled then
            criteria[#criteria + 1] = { name = boss, done = true, split = 402 * i, best = 395 * i }
        else
            criteria[#criteria + 1] = { name = boss, quantity = 0, total = 1 }
        end
    end
    criteria[#criteria + 1] = { weighted = true, percent = 64.3 }
    return criteria
end

local function previewAffixes()
    local names = {}
    for _, id in ipairs(T.DB:Data().affixes or {}) do
        local name = C_ChallengeMode and C_ChallengeMode.GetAffixInfo and C_ChallengeMode.GetAffixInfo(id)
        if name then names[#names + 1] = name end
    end
    if #names > 0 then return names end
    return { L["Example affix"], L["Second example affix"] }
end

function Timer.Preview()
    local level, name, limit = previewKey()
    level = level or EXAMPLE_LEVEL
    name = name or L["Example Dungeon"]
    limit = (limit and limit > 0) and limit or EXAMPLE_LIMIT

    local elapsed = math.floor(limit * EXAMPLE_PACE)
    local score = Timer.Score(level, elapsed, limit)
    return {
        level = level, name = name,
        affixes = previewAffixes(),
        elapsed = elapsed, limit = limit, deaths = 3, timeLost = 15,
        rating = T.Snapshot:Rating(),
        bestScore = math.floor(score * 0.87),
        criteria = previewCriteria(name),
    }
end

function Timer.ForgetSample() drawn = nil end

local function elapsedTime()
    for _, id in ipairs({ GetWorldElapsedTimers() }) do
        local _, elapsed, kind = GetWorldElapsedTime(id)
        if Plain(kind) == CHALLENGE_TIMER then return Plain(elapsed) end
    end
end

local function criteriaCount()
    if C_ScenarioInfo and C_ScenarioInfo.GetScenarioStepInfo then
        local info = C_ScenarioInfo.GetScenarioStepInfo()
        return info and Plain(info.numCriteria) or 0
    end
    local _, _, count = C_Scenario.GetStepInfo()
    return Plain(count) or 0
end

local function criterion(index)
    local info = C_ScenarioInfo and C_ScenarioInfo.GetCriteriaInfo and C_ScenarioInfo.GetCriteriaInfo(index)
    if not info then return nil end
    local c = {
        name = Plain(info.description) or "?",
        done = Plain(info.completed) and true or false,
        quantity = Plain(info.quantity),
        total = Plain(info.totalQuantity),
        weighted = Plain(info.isWeightedProgress) and true or false,
    }
    if c.weighted then
        local raw = tonumber((Plain(info.quantityString) or ""):match("^%s*(%d+)"))
        c.percent = (raw and (c.total or 0) > 0) and raw / c.total * 100 or (c.quantity or 0)
        if c.done and c.percent < 100 then c.percent = 100 end
    end
    return c
end

local function keystone()
    local mapId = Plain(C_ChallengeMode.GetActiveChallengeMapID())
    local level, affixIds = C_ChallengeMode.GetActiveKeystoneInfo()
    level = Plain(level)
    if mapId and level and level > 0 then return mapId, level, affixIds, true end

    if C_ChallengeMode.GetSlottedKeystoneInfo then
        local slotMap, slotAffixes, slotLevel = C_ChallengeMode.GetSlottedKeystoneInfo()
        slotMap, slotLevel = Plain(slotMap), Plain(slotLevel)
        if slotMap and slotLevel and slotLevel > 0 then
            return slotMap, slotLevel, slotAffixes, false
        end
    end
    return nil
end

local function read()
    local mapId, level, affixIds, running = keystone()
    if not mapId then return nil end

    local name, _, limit = C_ChallengeMode.GetMapUIInfo(mapId)
    local elapsed = 0
    if running then elapsed = elapsedTime() end
    local state = { mapId = mapId, level = level, name = Plain(name), limit = Plain(limit),
        elapsed = elapsed, running = running,
        affixes = {}, criteria = {} }

    for _, id in ipairs(type(affixIds) == "table" and affixIds or {}) do
        local affix = Plain(C_ChallengeMode.GetAffixInfo(id))
        if affix then state.affixes[#state.affixes + 1] = affix end
    end
    for i = 1, criteriaCount() do
        state.criteria[#state.criteria + 1] = criterion(i)
    end
    local deaths, lost = C_ChallengeMode.GetDeathCount()
    state.deaths, state.timeLost = Plain(deaths), Plain(lost)

    state.rating = T.Snapshot:Rating()

    local summary = C_PlayerInfo and C_PlayerInfo.GetPlayerMythicPlusRatingSummary
        and C_PlayerInfo.GetPlayerMythicPlusRatingSummary("player")
    for _, run in ipairs(summary and summary.runs or {}) do
        if Plain(run.challengeModeID) == mapId then state.bestScore = Plain(run.mapScore) end
    end
    return state
end

local primed = false

local function track(state)
    local store = T.DB:Data().timer
    local run = store.run
    if not run or run.mapId ~= state.mapId
        or (not run.finished and (run.level ~= state.level
            or (state.running and state.elapsed and state.elapsed + 5 < (run.elapsed or 0)))) then
        run = { mapId = state.mapId, level = state.level, splits = {} }
        store.run = run
    end
    state.elapsed = run.finished or state.elapsed or run.elapsed
    state.finished = run.finished ~= nil
    run.elapsed = state.elapsed

    local open = false
    for _, c in ipairs(state.criteria) do
        if not c.done then open = true end
    end
    if open and not run.finished and state.bestScore then
        run.bestScore = math.max(run.bestScore or 0, state.bestScore)
    end
    state.bestScore = run.bestScore or state.bestScore

    local dungeon = store.best[state.mapId] or {}
    store.best[state.mapId] = dungeon
    local bests = dungeon[state.level] or {}
    dungeon[state.level] = bests

    if run.holder == nil and state.running and not run.finished then
        local mapId, level = T.Snapshot:OwnKey()
        run.holder = mapId == state.mapId and level == state.level
    end

    for i, c in ipairs(state.criteria) do
        if c.done and run.splits[i] == nil and (state.elapsed or not primed) then
            local split = primed and state.elapsed or false
            run.splits[i] = split
            if split and (not bests[i] or split < bests[i]) then bests[i] = split end
            if split and not c.weighted then Timer.SayBoss(c, split, state) end
        end
        c.split = run.splits[i] or nil
        c.best = bests[i]
    end
    primed = true
end

local function finish()
    local run = T.DB:Data().timer.run
    if not run then return end
    local info = C_ChallengeMode.GetChallengeCompletionInfo and C_ChallengeMode.GetChallengeCompletionInfo()
    local ms = info and Plain(info.time)
    run.finished = (ms and ms > 0) and math.floor(ms / 1000) or elapsedTime() or run.elapsed
end

local frame, ticker
local rows = {}

local previewFor = {}
local function previewing() return next(previewFor) ~= nil end

local function colorTexture(texture, color)
    texture:SetColorTexture(color[1], color[2], color[3], color[4] or 1)
end

local function gradient(texture, low, high)
    if texture.SetGradient and CreateColor then
        texture:SetColorTexture(1, 1, 1, 1)
        texture:SetGradient("HORIZONTAL", CreateColor(low[1], low[2], low[3], 1),
            CreateColor(high[1], high[2], high[3], 1))
    else
        colorTexture(texture, high)
    end
end

local function widgets(i)
    local row = rows[i]
    if row then return row end
    local function fs(justify)
        local string = frame:CreateFontString(nil, "OVERLAY")
        string:SetJustifyH(justify)
        return string
    end
    row = {
        marker = frame:CreateTexture(nil, "OVERLAY"),
        left = fs("LEFT"), label = fs("LEFT"), right = fs("RIGHT"),
        rule = frame:CreateTexture(nil, "ARTWORK"),
        trackEdge = frame:CreateTexture(nil, "BORDER"),
        track = frame:CreateTexture(nil, "ARTWORK"),
        fill = frame:CreateTexture(nil, "OVERLAY"),
        marks = { frame:CreateTexture(nil, "OVERLAY"), frame:CreateTexture(nil, "OVERLAY") },
    }
    rows[i] = row
    return row
end

local function hideRow(row)
    row.marker:Hide() row.left:Hide() row.label:Hide() row.right:Hide()
    row.rule:Hide() row.trackEdge:Hide() row.track:Hide() row.fill:Hide()
    for _, mark in ipairs(row.marks) do mark:Hide() end
end

local function ratio()
    local mine = frame:GetEffectiveScale()
    local theirs = UIParent:GetEffectiveScale()
    if not (mine and theirs) or mine == 0 or theirs == 0 then return 1 end
    return mine / theirs
end

local function trackerSpot()
    for _, tracker in ipairs({ ObjectiveTrackerFrame, ScenarioObjectiveTracker }) do
        if tracker and tracker.GetLeft and tracker.GetEffectiveScale then
            local left, top = tracker:GetLeft(), tracker:GetTop()
            local mine, theirs = tracker:GetEffectiveScale(), UIParent:GetEffectiveScale()
            if left and top and mine and theirs and theirs ~= 0 then
                return left * mine / theirs, top * mine / theirs
            end
        end
    end
    return nil
end

local function place()
    local settings = T.DB:Settings().timer
    if not (settings.x and settings.y) then
        settings.x, settings.y = trackerSpot()
    end
    frame:ClearAllPoints()
    if settings.x and settings.y then
        local per = ratio()
        frame:SetPoint("TOPLEFT", UIParent, "BOTTOMLEFT", settings.x / per, settings.y / per)
    else
        frame:SetPoint("TOPRIGHT", UIParent, "TOPRIGHT", -90, -300)
    end
end

local function create()
    frame = CreateFrame("Frame", "RaidexKeysTimer", UIParent)
    frame:SetSize(200, 100)
    frame:SetFrameStrata("LOW")
    frame:SetClampedToScreen(true)
    frame:SetMovable(true)
    frame:EnableMouse(true)
    frame:RegisterForDrag("LeftButton")
    frame:SetScript("OnMouseDown", function(_, button)
        if button == "LeftButton" and T.Addon:ShiftDown() then Timer:SetShown(false) end
    end)
    frame:SetScript("OnDragStart", T.BeginDrag)
    frame:SetScript("OnDragStop", function(self)
        T.EndDrag(self)
        local left, top = self:GetLeft(), self:GetTop()
        if left and top then
            local settings = T.DB:Settings().timer
            local per = ratio()
            settings.x, settings.y = left * per, top * per
            place()
        end
    end)

    frame.card = frame:CreateTexture(nil, "BACKGROUND")
    frame.card:SetAllPoints()

    local edges = {}
    for i = 1, 4 do
        edges[i] = frame:CreateTexture(nil, "BORDER")
    end
    frame.edges = edges
    edges[1]:SetPoint("TOPLEFT")    edges[1]:SetPoint("TOPRIGHT")    edges[1]:SetHeight(1)
    edges[2]:SetPoint("BOTTOMLEFT") edges[2]:SetPoint("BOTTOMRIGHT") edges[2]:SetHeight(1)
    edges[3]:SetPoint("TOPLEFT")    edges[3]:SetPoint("BOTTOMLEFT")  edges[3]:SetWidth(1)
    edges[4]:SetPoint("TOPRIGHT")   edges[4]:SetPoint("BOTTOMRIGHT") edges[4]:SetWidth(1)

    place()
    frame:Hide()
end

local function render(lines)
    if not frame then create() end
    if not C.card then Timer.Style() end

    colorTexture(frame.card, C.card)
    for _, edge in ipairs(frame.edges) do colorTexture(edge, C.edge) end
    local markerSize = math.max(4, math.floor(MARKER_SIZE * SIZES.body / BODY + 0.5))

    local width = MIN_WIDTH
    for i, line in ipairs(lines) do
        local row = widgets(i)
        hideRow(row)
        if line.kind ~= "rule" and line.kind ~= "bar" then
            local size = SIZES[line.size] or SIZES.body
            setFont(row.left, size, line.display)
            row.left:SetText(line.left or "")
            row.left:SetWidth(0)
            local needed = line.wrap and 0 or row.left:GetStringWidth()
            if line.marker then needed = needed + MARKER_W + MARKER_GAP end
            if line.label then
                setFont(row.label, SIZES.small, false)
                row.label:SetText(line.label)
                needed = needed + 8 + row.label:GetStringWidth()
            end
            if line.right then
                setFont(row.right, size, false)
                row.right:SetText(line.right)
                needed = needed + COLUMN_GAP + row.right:GetStringWidth()
            end
            width = math.max(width, needed)
        end
    end

    local y = PADY
    for i, line in ipairs(lines) do
        local row = rows[i]
        y = y + math.floor((line.gap or 0) * SPACING + 0.5)
        if line.kind == "rule" then
            colorTexture(row.rule, line.accent and C.accent or C.rule)
            row.rule:ClearAllPoints()
            row.rule:SetPoint("TOPLEFT", 1, -y)
            row.rule:SetPoint("TOPRIGHT", -1, -y)
            row.rule:SetHeight(1)
            row.rule:Show()
            y = y + 1
        elseif line.kind == "bar" then
            local height = line.height or BAR_H
            colorTexture(row.trackEdge, C.trackEdge)
            row.trackEdge:ClearAllPoints()
            row.trackEdge:SetPoint("TOPLEFT", PADX, -y)
            row.trackEdge:SetSize(width, height)
            row.trackEdge:Show()

            colorTexture(row.track, C.track)
            row.track:ClearAllPoints()
            row.track:SetPoint("TOPLEFT", PADX + 1, -(y + 1))
            row.track:SetSize(width - 2, height - 2)
            row.track:Show()

            local fill = math.max(0, math.min(1, line.fill or 0))
            if fill > 0 then
                if line.tone == "glow" then gradient(row.fill, C.glowLow, C.glowHigh)
                else gradient(row.fill, C.goldLow, C.goldHigh) end
                row.fill:ClearAllPoints()
                row.fill:SetPoint("TOPLEFT", PADX + 1, -(y + 1))
                row.fill:SetSize(math.max(1, (width - 2) * fill), height - 2)
                row.fill:Show()
            end

            for m, share in ipairs(line.marks or {}) do
                local mark = row.marks[m]
                colorTexture(mark, m == 1 and C.markLow or C.markHigh)
                mark:ClearAllPoints()
                mark:SetPoint("TOPLEFT", PADX + width * share, -(y - 3))
                mark:SetSize(1, height + 6)
                mark:Show()
            end
            y = y + height
        else
            if line.wrap then
                row.left:SetWidth(width - (line.marker and MARKER_W + MARKER_GAP or 0))
                row.left:SetWordWrap(true)
            end
            local height = row.left:GetStringHeight()
            local x = PADX
            if line.marker then
                colorTexture(row.marker, line.marker == "done" and C.markerDone or C.markerOpen)
                row.marker:ClearAllPoints()
                row.marker:SetPoint("TOPLEFT", x + math.floor((MARKER_W - markerSize) / 2),
                    -(y + math.floor((height - markerSize) / 2)))
                row.marker:SetSize(markerSize, markerSize)
                row.marker:Show()
                x = x + MARKER_W + MARKER_GAP
            end
            row.left:ClearAllPoints()
            row.left:SetPoint("TOPLEFT", x, -y)
            row.left:Show()
            if line.label then
                row.label:ClearAllPoints()
                row.label:SetPoint("BOTTOMLEFT", row.left, "BOTTOMRIGHT", 8, 2)
                row.label:Show()
            end
            if line.right then
                row.right:ClearAllPoints()
                row.right:SetPoint("BOTTOMRIGHT", frame, "TOPRIGHT", -PADX, -(y + height))
                row.right:Show()
            end
            y = y + height + math.floor(LEADING * SPACING + 0.5)
        end
    end
    for i = #lines + 1, #rows do hideRow(rows[i]) end

    frame:SetSize(width + PADX * 2, y + PADY)
    if math.abs(frame:GetScale() - SCALE) > 0.001 then
        frame:SetScale(SCALE)
        place()
    end
    frame:Show()
    Timer.lines = lines
end

local blizzardFaded = false
local function fadeBlizzard(fade)
    local tracker = ScenarioObjectiveTracker
    if not (tracker and tracker.SetAlpha) then return end
    if fade then
        if tracker:GetAlpha() > 0 then tracker:SetAlpha(0) end
        blizzardFaded = true
    elseif blizzardFaded then
        tracker:SetAlpha(1)
        blizzardFaded = false
    end
end

local QUEST_TRACKERS = {
    "QuestObjectiveTracker", "CampaignQuestObjectiveTracker",
    "AchievementObjectiveTracker", "BonusObjectiveTracker",
    "WorldQuestObjectiveTracker", "ProfessionsRecipeTracker",
    "MonthlyActivitiesObjectiveTracker", "AdventureObjectiveTracker",
    "UIWidgetObjectiveTracker",
}

local function questTrackers()
    local found = {}
    local function take(region)
        if region and region.SetAlpha and region ~= ScenarioObjectiveTracker then
            found[#found + 1] = region
        end
    end

    local tracker = ObjectiveTrackerFrame
    take(tracker and tracker.Header)

    local modules = 0
    for _, module in ipairs(tracker and (tracker.modules or tracker.MODULES) or {}) do
        take(module)
        modules = modules + 1
    end
    if modules > 0 then return found end

    for _, name in ipairs(QUEST_TRACKERS) do take(_G[name]) end
    return found
end

local questsFaded = false
local function fadeQuests(fade)
    if not fade and not questsFaded then return end
    for _, module in ipairs(questTrackers()) do module:SetAlpha(fade and 0 or 1) end
    questsFaded = fade
end

function Timer.Projected(state, elapsed, limit)
    if not (limit and limit > 0) then return state.rating or 0, 0 end
    local score = Timer.Score(state.level, elapsed, limit)
    local gain = math.max(0, math.floor(score - (state.bestScore or 0) + 0.5))
    return (state.rating or 0) + gain, gain
end

local function chatLocked()
    if C_ChatInfo and C_ChatInfo.InChatMessagingLockdown then
        local ok, locked = pcall(C_ChatInfo.InChatMessagingLockdown)
        if ok and locked then return true end
    end
    local restriction = Enum and Enum.AddOnRestrictionType and Enum.AddOnRestrictionType.Chat
    if restriction and C_RestrictedActions and C_RestrictedActions.IsAddOnRestrictionActive then
        local ok, active = pcall(C_RestrictedActions.IsAddOnRestrictionActive, restriction)
        if ok and active then return true end
    end
    return false
end

local function sayToGroup(text)
    if IsInGroup() and not chatLocked() and pcall(C_ChatInfo.SendChatMessage, text, "PARTY") then return end
    T.Addon:Print(text)
end

local FREE_WAIT, FREE_LOOKS = 2, 30

local function sayToGroupWhenFree(text, looks)
    looks = looks or 0
    if IsInGroup and IsInGroup() and chatLocked() and looks < FREE_LOOKS then
        C_Timer.After(FREE_WAIT, function() sayToGroupWhenFree(text, looks + 1) end)
        return
    end
    sayToGroup(text)
end

local END_WAIT, END_LOOKS = 1, 8

function Timer.SayEnd(state)
    local run = T.DB:Data().timer.run
    local settings = T.DB:Settings().timer
    if not (run and run.holder and settings.enabled and settings.announce) then return end
    local info = C_ChallengeMode.GetChallengeCompletionInfo and C_ChallengeMode.GetChallengeCompletionInfo()
    local onTime = info and Plain(info.onTime)
    if onTime == nil and state and state.limit and run.finished then onTime = run.finished <= state.limit end
    if not onTime then
        sayToGroupWhenFree(L["Maybe next time!"])
        return
    end
    local looks = 0
    local function look()
        looks = looks + 1
        local mapId, level = T.Snapshot:OwnKey()
        local link = T.MinimapButton.KeystoneLink()
        local upgraded = link and mapId and (mapId ~= run.mapId or level ~= run.level)
        if upgraded then
            sayToGroupWhenFree(L["Well done! New keystone: %s"]:format(link))
        elseif looks >= END_LOOKS then
            sayToGroupWhenFree(L["Well done!"])
        else
            C_Timer.After(END_WAIT, look)
        end
    end
    look()
end

function Timer.SayBoss(criterion, split, state)
    local settings = T.DB:Settings().timer
    if not (settings.enabled and settings.bossTimes) then return end
    local rating, gain = Timer.Projected(state, split, state.limit)
    T.Addon:Print(L["%s in %s minutes, projected rating: %d (+%d)"]:format(
        criterion.name or "?", Timer.Clock(split), rating, gain))
end

local lastSeen

local held

local function ticking(on)
    if on and not ticker then
        ticker = C_Timer.NewTicker(TICK, function() Timer:Update() end)
    elseif not on and ticker then
        ticker:Cancel()
        ticker = nil
    end
end

function Timer.CompleteRun()
    local state = read() or lastSeen
    if not state then return end
    if lastSeen and #state.criteria < #lastSeen.criteria then state.criteria = lastSeen.criteria end
    for _, c in ipairs(state.criteria) do
        if not c.done then
            c.done = true
            if c.weighted then c.percent = math.max(c.percent or 0, 100) end
        end
    end
    track(state)
    held = state
end

function Timer:Update()
    local settings = T.DB:Settings().timer
    if not settings.enabled then
        held = nil
        fadeBlizzard(false)
        fadeQuests(false)
        ticking(false)
        if frame then frame:Hide() end
        Timer.lines = nil
        return
    end

    local state = read()
    if state then
        if held and #state.criteria < #held.criteria then state.criteria = held.criteria end
        track(state)
        if #state.criteria > 0 then lastSeen = state end
        held = state.finished and state or nil
    else
        if held and not (IsInInstance and IsInInstance()) then held = nil end
        state = held
    end
    local away = settings.hidden == true and not previewing()
    fadeBlizzard(state ~= nil and settings.hideBlizzard and not away)
    fadeQuests(state ~= nil and settings.hideQuests == true and not away)

    if previewing() then
        render(Timer.Lines(state or Timer.Preview()))
    elseif state and not away then
        render(Timer.Lines(state))
    else
        if frame then frame:Hide() end
        Timer.lines = nil
    end
    ticking(state ~= nil and not state.finished)
end

local STRATA = { "BACKGROUND", "LOW", "MEDIUM", "HIGH", "DIALOG", "FULLSCREEN", "FULLSCREEN_DIALOG" }

local function above(other)
    local its = other and other.GetFrameStrata and other:GetFrameStrata()
    for i, name in ipairs(STRATA) do
        if name == its then return STRATA[math.min(i + 1, #STRATA)] end
    end
    return "DIALOG"
end

local function setPreview(reason, on)
    previewFor[reason] = on or nil
    if not frame then create() end
    if not previewing() then Timer.ForgetSample() end
    frame:SetFrameStrata(previewFor.options and above(SettingsPanel) or "LOW")
    Timer:Update()
end

function Timer:SetPreview(on)
    setPreview("command", on)
end

function Timer:SetOptionsPreview(on)
    setPreview("options", on)
end

function Timer.Wanted()
    local settings = T.DB:Settings().timer
    return settings.enabled == true and not settings.hidden
end

function Timer:SetShown(on, quiet)
    local settings = T.DB:Settings().timer
    if Timer.Wanted() == on then return end
    settings.hidden = not on or nil
    if on and not settings.enabled then
        if T.SetTimerEnabled then
            T.SetTimerEnabled(true)
        else
            settings.enabled = true
            self:Update()
        end
    else
        self:Update()
    end
    if not on and not quiet then
        T.Addon:Print(T.DB:Settings().minimap.hide
            and L["Timer hidden. It comes back with the next key in the font, or with /rk timer."]
            or L["Timer hidden. Ctrl-click the minimap button to bring it back - the next key in the font does it too."])
    end
end

function Timer:TogglePreview()
    if not Timer.Wanted() then
        self:SetShown(true)
        T.Addon:Print(L["The Mythic+ timer is back."])
        return
    end
    local on = not previewFor.command
    setPreview("command", on)
    T.Addon:Print(previewing() and L["Timer preview: drag it into place, then type /rk timer again."]
        or L["Timer preview closed."])
end

local EVENTS = {
    "PLAYER_ENTERING_WORLD",
    "ZONE_CHANGED_NEW_AREA",
    "START_TIMER",
    "CHALLENGE_MODE_RESET",
    "CHALLENGE_MODE_DEATH_COUNT_UPDATED",
    "SCENARIO_CRITERIA_UPDATE",
    "SCENARIO_POI_UPDATE",
    "WORLD_STATE_TIMER_START",
    "WORLD_STATE_TIMER_STOP",
}

function Timer:ResetPosition()
    local settings = T.DB:Settings().timer
    settings.x, settings.y = nil, nil
    if frame then place() end
end

function Timer:ResetLook()
    local settings = T.DB:Settings().timer
    settings.alpha, settings.font, settings.fontSize = DEFAULT_ALPHA, DEFAULT_FONT, DEFAULT_SIZE
    settings.spacing = DEFAULT_SPACING
    settings.showAffixes, settings.showScore = DEFAULT_AFFIXES, DEFAULT_SCORE
    settings.maxBosses = DEFAULT_BOSSES
    settings.scale = DEFAULT_SCALE
    local back, text = T.Themes.TimerDefaults()
    settings.background = { back[1], back[2], back[3] }
    settings.text = { text[1], text[2], text[3] }
    self:Restyle()
end

function Timer:Restyle()
    Timer.Style()
    self:Update()
end

function Timer:Start()
    Timer.Style()
    for _, event in ipairs(EVENTS) do
        T.Addon:On(event, function() self:Update() end)
    end
    T.Addon:On("CHALLENGE_MODE_KEYSTONE_SLOTTED", function()
        T.DB:Settings().timer.hidden = nil
        self:Update()
    end)
    T.Addon:On("CHALLENGE_MODE_START", function()
        T.DB:Data().timer.run = nil
        lastSeen, held = nil, nil
        T.DB:Settings().timer.hidden = nil
        self:Update()
    end)
    T.Addon:On("CHALLENGE_MODE_COMPLETED", function()
        if not T.DB:Settings().timer.enabled then return end
        self:Update()
        finish()
        Timer.CompleteRun()
        self:Update()
        Timer.SayEnd(held)
    end)
    self:Update()
end
