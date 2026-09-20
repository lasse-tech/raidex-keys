local _, T = ...
local L = T.L

local Window = {}
T.Window = Window

local C = T.Themes.Colors
local textColors, textureColors = {}, {}

local BASE_SIZE = 13
Window.SIZE_MIN, Window.SIZE_MAX, Window.DEFAULT_SIZE = 10, 18, 12

local BASE = {
    W = 620,
    HEADER = 40, INFO = 24, TABS_H = 26, HEAD_H = 20, ROW_H = 22, GROUP_H = 26, FOOTER_H = 26,
    TALL_H = 52, NAME_Y = 26, BOSS_Y = 39,
    TABS_GAP = 6,
    PAD = 14, ACCENT = 7, COL_L = 16, COL_R = 16, GAP = 7,
    TAB_W = 76, ICON = 20, OPTIONS_H = 18, ARROW = 8, GRIP = 16,
    DOT = 6, DOT_GAP = 2, SLOT = 9, SLOT_GAP = 4,
    MARK = 12, MARK_GAP = 5,
    NAME_MAX = 210,
    BAR_W = 4, BAR_HIT = 10, THUMB_MIN = 16,
    EMPTY_HINT = 300, EMPTY_ACTION = 240,
}

local BASE_COLW = { name = 140, dungeon = 122, key = 34, week = 54, vault = 62, sync = 60, rating = 52,
    difficulty = 76 }

local ROWS, MIN_ROWS = 12, 4
local DOTS = 7
local ICON = "Interface\\AddOns\\RaidexKeys\\Media\\Icon"
local SORTABLE = { name = true, dungeon = true, key = true, week = true, vault = true, sync = true, rating = true }

local TABS = { "guild", "chars", "party", "goal", "board", "raids" }
local SORTED = { guild = true, chars = true, party = true }

local HEADING_FOR = {
    goal = { name = "Key holder", rating = "Gain" },
    board = { sync = "Time", name = "Holder", rating = "Signed up" },
    raids = { sync = "Time", name = "Raid", dungeon = "Signed up as", rating = "Signed up" },
}

local M, factor = { COLW = {} }, nil
local dims = { w = 0, h = 0 }

local function fontSize()
    local size = T.DB:Settings().window.fontSize or BASE_SIZE
    return math.max(Window.SIZE_MIN, math.min(Window.SIZE_MAX, size))
end

local function round(value)
    return math.floor(value + 0.5)
end

local function metrics()
    local f = fontSize() / BASE_SIZE
    if f == factor then return f end
    factor = f
    for key, value in pairs(BASE) do M[key] = round(value * f) end
    for key, value in pairs(BASE_COLW) do M.COLW[key] = round(value * f) end
    return f
end

local function chromeHeight()
    return M.HEADER + M.INFO + M.TABS_GAP * 2 + M.TABS_H + M.HEAD_H + M.FOOTER_H
end

local function minSize()
    local widest = 0
    for _, name in ipairs(TABS) do
        local cols, fixed = Window.Columns(name), 0
        for _, col in ipairs(cols) do fixed = fixed + M.COLW[col] end
        widest = math.max(widest, fixed + M.GAP * (#cols - 1))
    end
    return M.COL_L + M.COL_R + widest, chromeHeight() + MIN_ROWS * M.ROW_H
end

local function stored()
    local f = metrics()
    local settings = T.DB:Settings().window
    local w, h = M.W, chromeHeight() + ROWS * M.ROW_H
    if settings.w and settings.h then w, h = settings.w * f, settings.h * f end
    local minW, minH = minSize()
    local maxW = UIParent.GetWidth and UIParent:GetWidth() or 4096
    local maxH = UIParent.GetHeight and UIParent:GetHeight() or 4096
    return round(math.max(minW, math.min(math.max(minW, maxW), w))),
        round(math.max(minH, math.min(math.max(minH, maxH), h)))
end

local function measure()
    dims.w, dims.h = stored()
end

function Window.Size()
    return stored()
end

local function mapName(mapId)
    if not mapId then return nil end
    local map = T.DB:Data().maps[mapId]
    if map and map.name then return map.name end
    return C_ChallengeMode and C_ChallengeMode.GetMapUIInfo and C_ChallengeMode.GetMapUIInfo(mapId)
end

local function byRating(a, b)
    if (a.level or 0) ~= (b.level or 0) then return (a.level or 0) > (b.level or 0) end
    return (a.rating or 0) > (b.rating or 0)
end

local function mainGuidFor(guid)
    local main = T.DB:Settings().main
    if main and T.DB:Data().chars[main] then return main end
    return guid or T.Plain(UnitGUID("player"))
end

function Window.SetMain(guid)
    T.DB:Settings().main = guid ~= T.Plain(UnitGUID("player")) and guid or nil
    Window:Refresh()
end

function Window.View(which)
    local function shows(what) return which == nil or which == what end
    local db = T.DB:Data()
    local guid = T.Plain(UnitGUID("player"))
    local player = T.Plain(UnitName("player"))
    local mainGuid = mainGuidFor(guid)
    local view = { chars = {}, party = T.Exchange:PartyKeys(), guild = T.Exchange:GuildView() }

    for charGuid, char in pairs(db.chars) do
        local key = char.key
        view.chars[#view.chars + 1] = {
            guid = charGuid, main = charGuid == mainGuid,
            name = char.name, realm = char.realm, classId = char.classId,
            rating = char.rating or 0,
            mapId = key and key.mapId, level = key and key.level,
            dungeon = mapName(key and key.mapId),
            vault = char.vault, week = char.week, runs = char.week and #char.week or 0,
            seenAt = char.updatedAt,
            own = charGuid == guid,
            runs = true,
        }
    end
    local seen = {}
    for _, row in ipairs(view.chars) do seen[row.name] = (seen[row.name] or 0) + 1 end
    for _, row in ipairs(view.chars) do
        if (seen[row.name] or 0) > 1 and row.realm then row.name = row.name .. "-" .. row.realm end
    end

    table.sort(view.chars, byRating)
    for i, entry in ipairs(view.party) do
        entry.dungeon = mapName(entry.mapId)
        entry.own = i == 1
    end
    table.sort(view.party, byRating)
    local guildKeys = view.guild and view.guild.keys or {}
    for _, entry in ipairs(guildKeys) do
        entry.dungeon = mapName(entry.mapId)
        entry.own = player ~= nil and entry.name == player
    end
    table.sort(guildKeys, byRating)

    local own = mainGuid and db.chars[mainGuid]
    view.goalName = own and own.name
    view.mainName = T.DB:Settings().main == mainGuid and own and own.name or nil
    if shows("goal") and own and own.best and next(db.maps)
        and (next(own.best) or (own.rating or 0) == 0) then
        local bests = {}
        for mapId in pairs(db.maps) do
            local run = own.best[mapId]
            bests[mapId] = run and run.score or 0
        end
        view.goalRating = math.floor((own.rating or 0) + 0.5)
        view.goals = Window.Goals(view, view.goalRating, bests, T.Rating.LevelCap(own.best))
    end
    if shows("board") then view.board = T.Board:View() end
    if shows("raids") then view.raids = T.Raids:View() end
    return view
end

local function keyHolder(view, mapId, level)
    local found
    local function consider(row)
        if row.mapId == mapId and row.level and row.level >= level
            and (not found or row.level < found.level) then
            found = row
        end
    end
    for _, row in ipairs(view.chars or {}) do consider(row) end
    for _, row in ipairs(view.party or {}) do
        if not row.own then consider(row) end
    end
    for _, row in ipairs(view.guild and view.guild.keys or {}) do
        if not row.own then consider(row) end
    end
    return found
end

function Window.Goals(view, rating, bests, cap, names)
    local entries = {}
    for _, goal in ipairs(T.Rating.GOALS) do
        local plan = T.Rating.Plan(rating, goal, bests, cap)
        local note
        if not plan then note = L["out of reach"]
        elseif #plan == 0 then note = L["reached"]
        else note = #plan == 1 and L["1 run"] or L["%d runs"]:format(#plan) end
        entries[#entries + 1] = { group = true, title = tostring(goal), note = note }
        for _, run in ipairs(plan or {}) do
            local holder = keyHolder(view, run.mapId, run.level)
            local label = holder and holder.name
            if holder and holder.level ~= run.level then
                label = ("%s (+%d)"):format(holder.name, holder.level)
            end
            entries[#entries + 1] = {
                plan = true, mapId = run.mapId, level = run.level,
                dungeon = (names and names[run.mapId]) or mapName(run.mapId) or "?",
                rating = run.gain, ratingText = ("+%d"):format(run.gain),
                name = label, classId = holder and holder.classId, classFile = holder and holder.classFile,
            }
        end
    end
    return entries
end

local function boardEntries(posts)
    local count = {}
    for _, post in ipairs(posts or {}) do
        local day = T.BoardPane.DayKey(post.at)
        count[day] = (count[day] or 0) + 1
    end
    local entries, current = {}, nil
    for _, post in ipairs(posts or {}) do
        local day = T.BoardPane.DayKey(post.at)
        if day ~= current then
            current = day
            entries[#entries + 1] = { group = true, title = T.BoardPane.DayText(post.at),
                note = count[day] == 1 and L["1 key"] or L["%d keys"]:format(count[day]) }
        end
        entries[#entries + 1] = {
            board = true, id = post.id, name = post.name, who = post.who, classFile = post.classFile,
            mapId = post.mapId, level = post.level, dungeon = mapName(post.mapId),
            syncText = T.BoardPane.TimeText(post.at),
            rating = #post.applicants,
            ratingText = T.Board.CountText(#post.applicants),
            own = post.own,
        }
    end
    return entries
end

local function raidEntries(raids)
    local count = {}
    for _, raid in ipairs(raids or {}) do
        local day = T.BoardPane.DayKey(raid.at)
        count[day] = (count[day] or 0) + 1
    end
    local entries, current = {}, nil
    for _, raid in ipairs(raids or {}) do
        local day = T.BoardPane.DayKey(raid.at)
        if day ~= current then
            current = day
            entries[#entries + 1] = { group = true, title = T.BoardPane.DayText(raid.at),
                note = count[day] == 1 and L["1 raid"] or L["%d raids"]:format(count[day]) }
        end
        entries[#entries + 1] = {
            raid = true, key = raid.key, name = raid.title, difficulty = raid.difficultyName,
            tall = true, instanceName = raid.instanceName, killed = raid.killed, bosses = raid.bosses,
            dungeon = raid.mine and (raid.mine.role and T.RaidPane.RoleName(raid.mine.role) or L["no role yet"]) or "-",
            syncText = T.BoardPane.TimeText(raid.at),
            rating = #raid.signups, ratingText = tostring(#raid.signups),
            own = raid.mine ~= nil,
        }
    end
    return entries
end

function Window.Rows(view, tab)
    local rows
    if tab == "chars" then rows = view.chars
    elseif tab == "party" then rows = view.party
    elseif tab == "goal" then rows = view.goals or {}
    elseif tab == "board" then rows = boardEntries(view.board)
    elseif tab == "raids" then rows = raidEntries(view.raids)
    else rows = view.guild and view.guild.keys or {} end
    return rows, view.sample == true
end

function Window.Grouped(tab, sort)
    if not SORTED[tab] then return true end
    return tab == "guild" and sort.col == "dungeon"
end

function Window.Columns(tab)
    if tab == "goal" then return { "dungeon", "key", "name", "rating" } end
    if tab == "board" then return { "sync", "name", "dungeon", "key", "rating" } end
    if tab == "raids" then return { "sync", "name", "difficulty", "dungeon", "rating" } end
    local cols = { "name", "dungeon" }
    cols[#cols + 1] = "key"
    if tab == "chars" then
        cols[#cols + 1] = "week"
        cols[#cols + 1] = "vault"
    end
    cols[#cols + 1] = "sync"
    cols[#cols + 1] = "rating"
    return cols
end

function Window.Layout(cols, width)
    metrics()
    width = width or (dims.w > 0 and dims.w) or stored()
    local fixed, hasDungeon = 0, false
    for _, col in ipairs(cols) do
        fixed = fixed + M.COLW[col]
        if col == "dungeon" then hasDungeon = true end
    end
    local slack = math.max(0, width - M.COL_L - M.COL_R - fixed - M.GAP * (#cols - 1))
    local extra = {}
    if hasDungeon then
        extra.name = math.min(math.floor(slack / 2), M.NAME_MAX - M.COLW.name)
        extra.dungeon = slack - extra.name
    else
        extra.name = slack
    end
    local pos, x = {}, M.COL_L
    for _, col in ipairs(cols) do
        local w = M.COLW[col] + (extra[col] or 0)
        pos[col] = { x = x, w = w }
        x = x + w + M.GAP
    end
    return pos
end

local vaultItemLevel

local function leads(row)
    if row.main ~= nil then return row.main == true end
    return row.own == true
end

local function sortValue(row, col)
    if col == "name" then return (row.name or ""):lower() end
    if col == "dungeon" then return (row.dungeon or ""):lower() end
    if col == "key" then return row.level or -1 end
    if col == "week" then return row.runs or (row.week and #row.week) or -1 end
    if col == "vault" then return vaultItemLevel(row.vault) or -1 end
    if col == "sync" then return row.seenAt or -1 end
    return row.rating or -1
end

local function sorter(sort)
    local col, up = sort.col, sort.dir == "asc"
    return function(a, b)
        local left, right = sortValue(a, col), sortValue(b, col)
        if left ~= right then
            if up then return left < right end
            return left > right
        end
        if (a.rating or 0) ~= (b.rating or 0) then return (a.rating or 0) > (b.rating or 0) end
        return (a.name or ""):lower() < (b.name or ""):lower()
    end
end

function Window.Entries(rows, tab, sort)
    if not SORTED[tab] then return rows end
    local list = {}
    for i, row in ipairs(rows) do list[i] = row end
    table.sort(list, sorter(sort))

    if not Window.Grouped(tab, sort) then return list end

    local count = {}
    for _, row in ipairs(list) do
        local dungeon = row.dungeon or "-"
        count[dungeon] = (count[dungeon] or 0) + 1
    end
    local entries, current = {}, nil
    for _, row in ipairs(list) do
        local dungeon = row.dungeon or "-"
        if dungeon ~= current then
            current = dungeon
            entries[#entries + 1] = { group = true, dungeon = dungeon, count = count[dungeon] }
        end
        entries[#entries + 1] = row
    end
    return entries
end

function Window.IsEmpty(view, tab, entries)
    if tab == "chars" then return #view.chars <= 1 end
    if tab == "goal" then return view.goals == nil end
    return #entries == 0
end

function Window.EmptyState(view, tab)
    if tab == "chars" then
        return {
            title = L["No other characters"],
            hint = L["Only this character is stored. Log in with an alt, or type /reload, and it stands here."],
            action = "/reload",
        }
    end
    if tab == "party" then
        return {
            title = L["not in a group"],
            hint = L["Once you are in a group, the addon asks the others for their keys."],
        }
    end
    if tab == "goal" then
        return {
            title = L["No runs known yet"],
            hint = L["The goals are planned on this character's best runs of the season. The game hands them out a few seconds after login."],
        }
    end
    if tab == "board" then
        if not view.board then
            return { title = L["not in a guild"], hint = L["The key board is shared with your guild."] }
        end
        return {
            title = L["No keys on the board"],
            hint = L["Pin your key for a day and a time, and guild members sign up for it."],
            action = L["Post your key"],
            form = true,
        }
    end
    if tab == "raids" then
        if not view.raids then
            return { title = L["not in a guild"], hint = L["The raid calendar shows the raids of your guild's calendar."] }
        end
        return {
            title = L["No raids in the guild calendar"],
            hint = L["Raids your guild enters in the game's calendar for the next two weeks stand here, and you sign up for them as tank, healer or damage dealer."],
        }
    end
    return {
        title = view.guild and L["No keys yet this week"] or L["not in a guild"],
        hint = L["Guild keys come through the LibKeystone protocol, as with BigWigs."],
        action = L["Share key with guild · Options"],
        options = true,
    }
end

function Window.FooterLeft(view, which)
    if which == "chars" then
        if #view.chars == 0 then return L["no characters stored yet"] end
        local count = L["%d characters"]:format(#view.chars)
        return view.mainName and ("%s · %s"):format(count, L["main: %s"]:format(view.mainName)) or count
    end
    if which == "party" then
        if #view.party == 0 then return L["not in a group"] end
        local known = 0
        for _, row in ipairs(view.party) do if row.level then known = known + 1 end end
        return ("%s · %s"):format(L["%d in the group"]:format(#view.party),
            L["%d / %d keys known"]:format(known, #view.party))
    end
    if which == "goal" then
        if not view.goals then return L["no runs known yet"] end
        local rating = L["M+ rating %d"]:format(view.goalRating or 0)
        return view.goalName and ("%s · %s"):format(view.goalName, rating) or rating
    end
    if which == "board" then
        if not view.board then return L["not in a guild"] end
        return #view.board == 1 and L["1 key on the board"] or L["%d keys on the board"]:format(#view.board)
    end
    if which == "raids" then
        if not view.raids then return L["not in a guild"] end
        return #view.raids == 1 and L["1 raid in the calendar"] or L["%d raids in the calendar"]:format(#view.raids)
    end
    if not view.guild then return L["not in a guild"] end
    local keys = #(view.guild.keys or {})
    return ("%s · %s"):format(view.guild.name,
        keys == 1 and L["1 key this week"] or L["%d keys this week"]:format(keys))
end

function Window.AffixText(view)
    if view.sample then
        return ("%s · %s"):format(L["Example affix"], L["Second example affix"])
    end
    local names = {}
    for _, id in ipairs(T.DB:Data().affixes or {}) do
        local name = C_ChallengeMode and C_ChallengeMode.GetAffixInfo
            and T.Plain(C_ChallengeMode.GetAffixInfo(id))
        if name then names[#names + 1] = name end
    end
    return table.concat(names, " · ")
end

function Window.ResetText()
    local seconds = C_DateAndTime and C_DateAndTime.GetSecondsUntilWeeklyReset
        and T.Plain(C_DateAndTime.GetSecondsUntilWeeklyReset())
    if not seconds or seconds <= 0 then return "" end
    local days = math.floor(seconds / 86400)
    local hours = math.floor((seconds % 86400) / 3600)
    if days > 0 then return L["%dd %dh"]:format(days, hours) end
    return L["%dh"]:format(math.max(1, hours))
end

function Window.Since(stamp)
    if not stamp then return "" end
    local seconds = GetServerTime() - stamp
    if seconds < 90 then return L["now"] end
    if seconds < 3600 then return L["%d min"]:format(math.floor(seconds / 60)) end
    if seconds < 86400 then return L["%d h"]:format(math.floor(seconds / 3600)) end
    if seconds < 2 * 86400 then return L["yesterday"] end
    return L["%d d"]:format(math.floor(seconds / 86400))
end

function vaultItemLevel(slots)
    local best
    for _, slot in ipairs(slots or {}) do
        if (slot.progress or 0) >= (slot.threshold or 0) and slot.itemLevel then
            best = math.max(best or 0, slot.itemLevel)
        end
    end
    return best
end

local function vaultSlots(row)
    local slots = {}
    for i = 1, 3 do
        local slot = row.vault and row.vault[i]
        slots[i] = slot and (slot.progress or 0) >= (slot.threshold or 0) or false
    end
    return slots
end

local function fileColor(file)
    if not file then return nil end
    if C_ClassColor and C_ClassColor.GetClassColor then
        local color = C_ClassColor.GetClassColor(file)
        if color then return { color.r, color.g, color.b } end
    end
    local color = RAID_CLASS_COLORS and RAID_CLASS_COLORS[file]
    if color then return { color.r, color.g, color.b } end
    return nil
end

local function classColor(classId, classFile)
    local info = classId and C_CreatureInfo and C_CreatureInfo.GetClassInfo
        and C_CreatureInfo.GetClassInfo(classId)
    return fileColor(info and info.classFile) or fileColor(classFile)
end

local frame, list, tabButtons, headings, sortArrow, entryFrames, empty, info, footer, optionsButton
local tab, offset, preview, hovered = "chars", 0, false, nil
local R = { listTop = 0, listH = 0 }

local function defaultSort(name)
    if name == "guild" then return { col = "dungeon", dir = "asc" } end
    if not SORTED[name] then return { col = "none", dir = "asc" } end
    return { col = "rating", dir = "desc" }
end

local sorts = {}
for _, name in ipairs(TABS) do sorts[name] = defaultSort(name) end

local leaving = false

local ticker

local function closed()
    if ticker then ticker:Cancel() ticker = nil end
    hovered = nil
    if preview and T.Preview then T.Preview:Stop() end
end

Window.FONTS = T.Fonts.List
Window.DEFAULT_FONT = "marcellus"

local fontStrings = {}

local function setFont(fs, size)
    local scaled = round(size * metrics())
    T.Fonts.Set(fs, T.DB:Settings().window.font, scaled, nil,
        size >= 16, Window.DEFAULT_FONT)
end

local function text(parent, size, color, justify)
    local fs = parent:CreateFontString(nil, "OVERLAY")
    setFont(fs, size)
    fs:SetTextColor(color[1], color[2], color[3])
    textColors[fs] = color
    fs:SetJustifyH(justify or "LEFT")
    fontStrings[#fontStrings + 1] = { fs = fs, size = size }
    return fs
end

local function fill(parent, color, alpha, layer)
    local texture = parent:CreateTexture(nil, layer or "BACKGROUND")
    texture:SetColorTexture(color[1], color[2], color[3], alpha or 1)
    textureColors[texture] = { color = color, alpha = alpha or 1 }
    return texture
end

local function tint(region, color)
    region:SetTextColor(color[1], color[2], color[3])
    textColors[region] = color
end

Window.C = C
Window.Text = text
Window.Fill = fill
Window.Tint = tint
Window.ClassColor = classColor
Window.MapName = mapName

function Window.SetHint(value)
    if footer then footer.hint:SetText(value or "") end
end

function Window.ListHeight()
    return R.listH
end

function Window.Metrics()
    local f = metrics()
    return M, f
end

function Window.Link(parent, label, onClick)
    local button = CreateFrame("Button", nil, parent)
    button.label = text(button, 12, C.gold, "CENTER")
    button.label:SetPoint("CENTER")
    button:SetScript("OnClick", onClick)
    button:SetScript("OnEnter", function() tint(button.label, C.text) end)
    button:SetScript("OnLeave", function() tint(button.label, C.gold) end)
    button.SetLabel = function(self, value)
        local f = metrics()
        self.label:SetText(value or "")
        self:SetSize(math.max(self.label:GetStringWidth(), 8) + round(12 * f), M.OPTIONS_H)
    end
    button:SetLabel(label)
    return button
end

function Window.Button(parent, label, onClick)
    local button = CreateFrame("Button", nil, parent)
    button.face = fill(button, C.tab, 1)
    button.face:SetAllPoints()
    button.edges = {}
    local function line(point, otherPoint, width, height)
        local texture = fill(button, C.edge, 1, "BORDER")
        texture:SetPoint(point)
        texture:SetPoint(otherPoint)
        if width then texture:SetWidth(width) else texture:SetHeight(height) end
        button.edges[#button.edges + 1] = texture
    end
    line("TOPLEFT", "TOPRIGHT", nil, 1)
    line("BOTTOMLEFT", "BOTTOMRIGHT", nil, 1)
    line("TOPLEFT", "BOTTOMLEFT", 1)
    line("TOPRIGHT", "BOTTOMRIGHT", 1)
    button.label = text(button, 12, C.gold, "CENTER")
    button.label:SetPoint("CENTER")

    local function paint(hover)
        local enabled = button:IsEnabled() ~= false
        local edge = hover and enabled and C.gold or C.edge
        for _, texture in ipairs(button.edges) do
            texture:SetColorTexture(edge[1], edge[2], edge[3], 1)
        end
        tint(button.label, not enabled and C.dim or hover and C.text or C.gold)
    end
    button:SetScript("OnClick", onClick)
    button:SetScript("OnEnter", function() paint(true) end)
    button:SetScript("OnLeave", function() paint(false) end)
    button:SetScript("OnEnable", function() paint(false) end)
    button:SetScript("OnDisable", function() paint(false) end)
    button.SetLabel = function(self, value)
        local f = metrics()
        self.label:SetText(value or "")
        self:SetSize(math.max(self.label:GetStringWidth(), 8) + round(20 * f), M.OPTIONS_H + round(6 * f))
    end
    button:SetLabel(label)
    return button
end

local function rule(parent, color)
    local texture = fill(parent, color, 1, "BORDER")
    texture:SetHeight(1)
    return texture
end

local function setRule(texture, y)
    texture:ClearAllPoints()
    texture:SetPoint("TOPLEFT", 1, -y)
    texture:SetPoint("TOPRIGHT", -1, -y)
end

local function edges(parent)
    local function line(point, otherPoint, width, height)
        local texture = fill(parent, C.edge, 1, "BORDER")
        texture:SetPoint(point)
        texture:SetPoint(otherPoint)
        if width then texture:SetWidth(width) else texture:SetHeight(height) end
    end
    line("TOPLEFT", "TOPRIGHT", nil, 1)
    line("BOTTOMLEFT", "BOTTOMRIGHT", nil, 1)
    line("TOPLEFT", "BOTTOMLEFT", 1)
    line("TOPRIGHT", "BOTTOMRIGHT", 1)
end

local function place()
    local settings = T.DB:Settings().window
    frame:ClearAllPoints()
    if settings.x and settings.y then
        frame:SetPoint("TOPLEFT", UIParent, "BOTTOMLEFT", settings.x, settings.y)
    else
        frame:SetPoint("CENTER", UIParent, "CENTER", 0, 40)
    end
end

local function remember()
    local left, top = frame:GetLeft(), frame:GetTop()
    if left and top then
        local settings = T.DB:Settings().window
        settings.x, settings.y = left, top
    end
end

local function createHeader()
    R.headerBar = fill(frame, C.header)
    R.headerBar:SetPoint("TOPLEFT", 1, -1)
    R.headerBar:SetPoint("TOPRIGHT", -1, -1)

    R.drag = CreateFrame("Frame", nil, frame)
    R.drag:SetFrameLevel(frame:GetFrameLevel() + 1)
    R.drag:SetPoint("TOPLEFT")
    R.drag:SetPoint("TOPRIGHT")
    R.drag:EnableMouse(true)
    R.drag:RegisterForDrag("LeftButton")
    R.drag:SetScript("OnDragStart", function() T.BeginDrag(frame) end)
    R.drag:SetScript("OnDragStop", function()
        T.EndDrag(frame)
        remember()
        place()
    end)

    R.icon = frame:CreateTexture(nil, "ARTWORK")
    R.icon:SetTexture(ICON, "CLAMP", "CLAMP", "TRILINEAR")

    local title = text(frame, 14, C.text)
    title:SetPoint("LEFT", R.icon, "RIGHT", 8, 0)
    title:SetText(L["Raidex Keys"]:upper())

    local options = CreateFrame("Button", nil, frame)
    options.label = text(options, 12, C.options, "CENTER")
    options.label:SetPoint("CENTER")
    options.label:SetText(L["Options"])
    options:SetFrameLevel(frame:GetFrameLevel() + 4)
    optionsButton = options
    options:SetScript("OnClick", function() T.OpenOptions() end)
    options:SetScript("OnEnter", function() tint(options.label, C.text) end)
    options:SetScript("OnLeave", function() tint(options.label, C.options) end)

    local close = CreateFrame("Button", nil, frame, "UIPanelCloseButton")
    close:SetFrameLevel(frame:GetFrameLevel() + 4)
    close:SetPoint("TOPRIGHT", 2, 2)
    close:SetScript("OnClick", function() Window:Close() end)
end

local function createInfo()
    R.infoBar = fill(frame, C.info)
    R.infoRule = rule(frame, C.divider)

    info = {}
    info.reset = text(frame, 12, C.gold, "RIGHT")
    info.resetLabel = text(frame, 10, C.muted, "RIGHT")
    info.resetLabel:SetPoint("RIGHT", info.reset, "LEFT", -8, 0)
    info.resetLabel:SetText(L["Reset in"]:upper())

    info.affixLabel = text(frame, 10, C.muted)
    info.affixLabel:SetText(L["Affixes"]:upper())

    info.affixes = text(frame, 10, C.tabText)
    info.affixes:SetPoint("LEFT", info.affixLabel, "RIGHT", 8, 0)
    info.affixes:SetPoint("RIGHT", info.resetLabel, "LEFT", -12, 0)
    info.affixes:SetWordWrap(false)
end

local function createTabs()
    R.tabBar = fill(frame, C.info, 1, "BORDER")
    R.tabsRule = rule(frame, C.divider)

    tabButtons = {}
    for _, name in ipairs(TABS) do
        local button = CreateFrame("Button", nil, frame)
        button.background = fill(button, C.tab)
        button.background:SetAllPoints()
        button.hover = fill(button, C.glow, 0.08, "BORDER")
        button.hover:SetAllPoints()
        button.hover:Hide()
        button:SetScript("OnEnter", function()
            if name == tab or button:IsEnabled() == false then return end
            button.hover:Show()
            tint(button.label, C.text)
        end)
        button:SetScript("OnLeave", function()
            button.hover:Hide()
            if name ~= tab then tint(button.label, button:IsEnabled() == false and C.dim or C.muted) end
        end)
        button.underline = fill(button, C.glow, 1, "ARTWORK")
        button.underline:SetPoint("BOTTOMLEFT")
        button.underline:SetPoint("BOTTOMRIGHT")
        button.underline:SetHeight(2)
        button.label = text(button, 12, C.muted, "CENTER")
        button.label:SetPoint("CENTER")
        button:SetScript("OnClick", function()
            button.hover:Hide()
            tab, offset = name, 0
            sorts[name] = defaultSort(name)
            T.DB:Settings().window.tab = name
            T.BoardPane:Close()
            T.RaidPane:Close()
            if name == "raids" then T.Raids:Load(true) end
            Window:Refresh()
        end)
        tabButtons[name] = button
    end
    R.postButton = Window.Button(frame, L["Post your key"], function() T.BoardPane:OpenForm() end)
    R.postButton:Hide()
end

local function createHeadings()
    headings = {}
    for column in pairs(BASE_COLW) do
        local button = CreateFrame("Button", nil, frame)
        local justify = column == "rating" and "RIGHT" or "LEFT"
        button.label = text(button, 10, C.muted, justify)
        button.label:SetPoint(justify)
        button.label:SetWordWrap(false)
        button:SetScript("OnClick", function()
            if not SORTED[tab] then return end
            local sort = sorts[tab]
            if sort.col == column then
                sort.dir = sort.dir == "desc" and "asc" or "desc"
            else
                sort.col, sort.dir = column, (column == "name" or column == "dungeon") and "asc" or "desc"
            end
            offset = 0
            Window:Refresh()
        end)
        button:SetScript("OnEnter", function()
            if SORTED[tab] and SORTABLE[column] then tint(button.label, C.tabText) end
        end)
        button:SetScript("OnLeave", function()
            tint(button.label, sorts[tab].col == column and C.tabText or C.muted)
        end)
        headings[column] = button
    end
    R.headRule = rule(frame, C.divider)

    sortArrow = frame:CreateTexture(nil, "OVERLAY")
    sortArrow:SetTexture("Interface\\ChatFrame\\ChatFrameExpandArrow")
    sortArrow:SetVertexColor(C.tabText[1], C.tabText[2], C.tabText[3])
    sortArrow:Hide()
end

local rowLift = 0

local function column(region, pos, name, justify)
    local box = pos[name]
    if not box then region:Hide() return nil end
    region:ClearAllPoints()
    if justify == "RIGHT" then
        region:SetPoint("RIGHT", box.x + box.w - dims.w + 1, rowLift)
    else
        region:SetPoint("LEFT", box.x - 1, rowLift)
    end
    if region.SetWidth then region:SetWidth(box.w) end
    region:Show()
    return box
end

local function linkKey(row)
    if not row.level then return end
    if row.own then
        local link = T.MinimapButton.KeystoneLink()
        if link then T.MinimapButton.LinkInChat(link) return end
    end
    T.MinimapButton.LinkInChat(("%s: %s +%d"):format(row.name or "?", row.dungeon or "?", row.level))
end

local function askMain(row)
    if not (StaticPopupDialogs and StaticPopup_Show) then
        Window.SetMain(row.guid)
        return
    end
    StaticPopupDialogs.RAIDEXKEYS_MAIN = StaticPopupDialogs.RAIDEXKEYS_MAIN or {
        text = "%s", button1 = YES, button2 = NO,
        OnAccept = function(_, guid) Window.SetMain(guid) end,
        timeout = 0, whileDead = true, hideOnEscape = true, preferredIndex = 3,
    }
    StaticPopup_Show("RAIDEXKEYS_MAIN",
        L["Make %s your main? The rating goals are then planned on this character's runs."]:format(row.name or "?"),
        nil, row.guid)
end

local function createEntry()
    local entry = CreateFrame("Frame", nil, list)
    entry:SetWidth(dims.w - 2)
    entry:EnableMouse(true)

    entry.hover = fill(entry, C.glow, 0.06)
    entry.hover:SetAllPoints()
    entry.accent = fill(entry, C.glow, 1, "ARTWORK")
    entry.accent:SetWidth(2)
    entry.line = fill(entry, C.rowLine, 1, "BORDER")
    entry.line:SetPoint("BOTTOMLEFT")
    entry.line:SetPoint("BOTTOMRIGHT")
    entry.line:SetHeight(1)

    entry.name = text(entry, 13, C.text)
    entry.name:SetWordWrap(false)
    entry.dungeon = text(entry, 13, C.body)
    entry.dungeon:SetWordWrap(false)
    entry.key = text(entry, 13, C.glow)
    entry.rating = text(entry, 13, C.text, "RIGHT")
    entry.sync = text(entry, 11, C.faint)
    entry.ilvl = text(entry, 11, C.muted, "RIGHT")
    entry.difficulty = text(entry, 13, C.body)
    entry.difficulty:SetWordWrap(false)
    entry.instance = text(entry, 11, C.muted)
    entry.instance:SetWordWrap(false)
    entry.bosses = {}

    entry.mark = entry:CreateTexture(nil, "OVERLAY")
    entry.mark:SetTexture(ICON, "CLAMP", "CLAMP", "TRILINEAR")
    entry.mark:Hide()

    entry.dots = {}
    for i = 1, DOTS do entry.dots[i] = entry:CreateTexture(nil, "ARTWORK") end

    entry.slots = {}
    for i = 1, 3 do entry.slots[i] = entry:CreateTexture(nil, "ARTWORK") end

    entry.groupName = text(entry, 12, C.body)
    entry.groupCount = text(entry, 11, C.muted, "RIGHT")
    entry.groupLine = fill(entry, C.divider, 1, "ARTWORK")
    entry.groupLine:SetHeight(1)

    entry:SetScript("OnEnter", function()
        local row = entry.row
        if not row then return end
        hovered = entry
        entry.hover:Show()
        if entry.marked then entry.mark:Show() end
        footer.hint:SetText((row.board or row.raid) and L["Click: details and sign-up"]
            or (row.guid and not row.main and not entry.sample) and L["Click: make this your main"] or "")
    end)
    entry:SetScript("OnLeave", function()
        if hovered == entry then hovered = nil end
        entry.hover:Hide()
        entry.mark:Hide()
        footer.hint:SetText("")
    end)
    entry:SetScript("OnMouseUp", function(_, button)
        local row = entry.row
        if not row or entry.sample then return end
        if row.board then
            if button == "LeftButton" then T.BoardPane:Open(row.id) end
        elseif row.raid then
            if button == "LeftButton" then T.RaidPane:Open(row.key) end
        elseif button == "LeftButton" and row.guid and not row.main then
            askMain(row)
        elseif button == "RightButton" and not row.plan then
            linkKey(row)
        end
    end)
    return entry
end

local function hideRow(entry)
    entry.mark:Hide()
    entry.dungeon:Hide()
    entry.key:Hide()
    entry.rating:Hide()
    entry.sync:Hide()
    entry.ilvl:Hide()
    entry.difficulty:Hide()
    entry.instance:Hide()
    for _, dot in ipairs(entry.dots) do dot:Hide() end
    for _, slot in ipairs(entry.slots) do slot:Hide() end
    for _, square in ipairs(entry.bosses) do square:Hide() end
end

local function drawGroup(entry, group)
    entry:SetHeight(M.GROUP_H)
    entry.row, entry.sample = nil, false
    entry.hover:Hide()
    entry.accent:Hide()
    entry.line:Hide()
    hideRow(entry)

    entry.name:Hide()
    entry.groupName:Show()
    entry.groupName:ClearAllPoints()
    entry.groupName:SetPoint("BOTTOMLEFT", M.COL_L - 1, round(5 * factor))
    entry.groupName:SetText(group.title or group.dungeon)

    entry.groupCount:Show()
    entry.groupCount:ClearAllPoints()
    entry.groupCount:SetPoint("BOTTOMRIGHT", -(M.COL_R - 1), round(6 * factor))
    entry.groupCount:SetText(group.note
        or (group.count == 1 and L["1 key"] or L["%d keys"]:format(group.count)))

    entry.groupLine:Show()
    entry.groupLine:ClearAllPoints()
    entry.groupLine:SetPoint("LEFT", entry.groupName, "RIGHT", 8, 1)
    entry.groupLine:SetPoint("RIGHT", entry.groupCount, "LEFT", -8, 1)
end

local function drawRow(entry, row, pos, sample)
    entry:SetHeight(row.tall and M.TALL_H or M.ROW_H)
    rowLift = row.tall and round((M.TALL_H - M.ROW_H) / 2) or 0
    entry.row, entry.sample = row, sample
    entry.line:Show()
    entry.groupName:Hide()
    entry.groupCount:Hide()
    entry.groupLine:Hide()
    entry.hover:SetShown(hovered == entry)
    entry.accent:ClearAllPoints()
    entry.accent:SetPoint("TOPLEFT", M.ACCENT, 0)
    entry.accent:SetPoint("BOTTOMLEFT", M.ACCENT, 0)
    entry.accent:SetShown(leads(row))

    column(entry.name, pos, "name")
    entry.name:SetText(row.name or (row.plan and "-" or "?"))
    local color = classColor(row.classId, row.classFile) or (row.plan and not row.name and C.dim) or C.text
    tint(entry.name, color)

    entry.marked = row.runs == true or (row.who ~= nil and T.Exchange:RunsAddon(row.who))
    if entry.marked then
        local width = math.min(entry.name:GetStringWidth() or 0, pos.name.w - M.MARK_GAP - M.MARK)
        entry.mark:SetSize(M.MARK, M.MARK)
        entry.mark:ClearAllPoints()
        entry.mark:SetPoint("LEFT", entry.name, "LEFT", math.max(0, width) + M.MARK_GAP, 0)
    end
    entry.mark:SetShown(entry.marked and hovered == entry)

    if column(entry.dungeon, pos, "dungeon") then
        entry.dungeon:SetText(row.dungeon or "-")
    end

    if column(entry.key, pos, "key") then
        entry.key:SetText(row.level and ("+%d"):format(row.level) or "-")
        tint(entry.key, row.level and C.glow or C.dim)
    end

    local box = pos.week
    for i, dot in ipairs(entry.dots) do
        local run = box and row.week and row.week[i]
        if run then
            dot:SetSize(M.DOT, M.DOT)
            dot:ClearAllPoints()
            dot:SetPoint("LEFT", box.x - 1 + (i - 1) * (M.DOT + M.DOT_GAP), 0)
            if run.timed then
                dot:SetColorTexture(C.glow[1], C.glow[2], C.glow[3], 1)
            else
                dot:SetColorTexture(C.muted[1], C.muted[2], C.muted[3], 0.30)
            end
            dot:Show()
        else
            dot:Hide()
        end
    end

    box = pos.vault
    local slots = box and vaultSlots(row)
    for i, slot in ipairs(entry.slots) do
        if slots then
            slot:SetSize(M.SLOT, M.SLOT)
            slot:ClearAllPoints()
            slot:SetPoint("LEFT", box.x - 1 + (i - 1) * (M.SLOT + M.SLOT_GAP), 0)
            if slots[i] then
                slot:SetColorTexture(C.vault[1], C.vault[2], C.vault[3], 1)
            else
                slot:SetColorTexture(C.muted[1], C.muted[2], C.muted[3], 0.12)
            end
            slot:Show()
        else
            slot:Hide()
        end
    end
    if box then
        local itemLevel = vaultItemLevel(row.vault)
        entry.ilvl:ClearAllPoints()
        entry.ilvl:SetPoint("RIGHT", box.x + box.w - dims.w + 1, 0)
        entry.ilvl:SetText(itemLevel and tostring(itemLevel) or "")
        entry.ilvl:Show()
    else
        entry.ilvl:Hide()
    end

    if column(entry.sync, pos, "sync") then
        entry.sync:SetText(row.syncText or Window.Since(row.seenAt))
    end

    if column(entry.rating, pos, "rating", "RIGHT") then
        entry.rating:SetText(row.ratingText or (row.rating and math.floor(row.rating + 0.5)) or "-")
    end

    if column(entry.difficulty, pos, "difficulty") then
        entry.difficulty:SetText(row.difficulty or "-")
    end

    local nameBox = pos.name
    local bossCount = row.tall and nameBox and row.bosses or 0
    if row.tall and nameBox and row.instanceName then
        entry.instance:ClearAllPoints()
        entry.instance:SetPoint("LEFT", nameBox.x - 1, round(M.TALL_H / 2) - M.NAME_Y)
        entry.instance:SetWidth(nameBox.w)
        entry.instance:SetText(row.instanceName)
        entry.instance:Show()
    else
        entry.instance:Hide()
    end
    for i = 1, math.max(bossCount, #entry.bosses) do
        local square = entry.bosses[i]
        if i <= bossCount then
            if not square then
                square = entry:CreateTexture(nil, "ARTWORK")
                entry.bosses[i] = square
            end
            square:SetSize(M.DOT, M.DOT)
            square:ClearAllPoints()
            square:SetPoint("LEFT", nameBox.x - 1 + (i - 1) * (M.DOT + M.DOT_GAP), round(M.TALL_H / 2) - M.BOSS_Y)
            if i <= (row.killed or 0) then
                square:SetColorTexture(C.glow[1], C.glow[2], C.glow[3], 1)
            else
                square:SetColorTexture(C.muted[1], C.muted[2], C.muted[3], 0.30)
            end
            square:Show()
        elseif square then
            square:Hide()
        end
    end

    entry:SetAlpha(sample and 0.55 or 1)
end

local function createList()
    list = CreateFrame("Frame", nil, frame)
    if list.SetClipsChildren then list:SetClipsChildren(true) end
    entryFrames = {}

    empty = CreateFrame("Frame", nil, list)
    empty:SetAllPoints()
    empty.title = text(empty, 14, C.body, "CENTER")
    empty.hint = text(empty, 12, C.muted, "CENTER")
    empty.hint:SetPoint("TOP", empty.title, "BOTTOM", 0, -10)
    empty.hint:SetSpacing(3)

    empty.action = Window.Button(empty, "", function()
        if empty.action.opens == "options" then
            T.OpenOptions()
        elseif empty.action.opens == "form" then
            T.BoardPane:OpenForm()
        end
    end)
    empty.action:SetPoint("TOP", empty.hint, "BOTTOM", 0, -10)
    empty.command = text(empty, 12, C.gold, "CENTER")
    empty.command:SetPoint("TOP", empty.hint, "BOTTOM", 0, -10)
    empty:Hide()

    T.BoardPane:Create(frame, list)
    T.RaidPane:Create(frame, list)
end

local function createFooter()
    R.footerRule = rule(frame, C.divider)

    footer = {}
    footer.left = text(frame, 12, C.muted)
    footer.hint = text(frame, 11, C.faint)
    footer.hint:SetPoint("LEFT", footer.left, "RIGHT", 12, 0)
    footer.right = text(frame, 12, C.gold, "RIGHT")
    footer.hint:SetPoint("RIGHT", footer.right, "LEFT", -12, 0)
    footer.hint:SetWordWrap(false)
end

local function entryHeight(item)
    return item.group and M.GROUP_H or item.tall and M.TALL_H or M.ROW_H
end

local function maxOffset(entries)
    local used = 0
    for i = #entries, 1, -1 do
        used = used + entryHeight(entries[i])
        if used > R.listH then return i end
    end
    return 0
end

local function pageSize(entries, first, step)
    local used, count, index = 0, 0, first
    while entries[index] do
        used = used + entryHeight(entries[index])
        if used > R.listH then break end
        count, index = count + 1, index + step
    end
    return math.max(1, count)
end

local scrollBar, thumb
local maxScroll, scrollRoom = 0, 0
local scrollEntries = {}

local function scrollTo(value)
    value = math.max(0, math.min(maxScroll, value))
    if value == offset then return end
    offset = value
    Window:Refresh()
end

local function cursorY(region)
    local _, y = GetCursorPosition()
    return y / region:GetEffectiveScale()
end

function Window.CursorY(region)
    return cursorY(region)
end

local function createScrollBar()
    scrollBar = CreateFrame("Frame", nil, frame)
    scrollBar:SetFrameLevel(frame:GetFrameLevel() + 5)
    scrollBar:SetPoint("TOPRIGHT", list, "TOPRIGHT", -3, -2)
    scrollBar:SetPoint("BOTTOMRIGHT", list, "BOTTOMRIGHT", -3, 2)
    scrollBar:EnableMouse(true)
    scrollBar:SetScript("OnMouseDown", function(_, button)
        if button ~= "LeftButton" then return end
        local y, top, bottom = cursorY(scrollBar), thumb:GetTop(), thumb:GetBottom()
        if top and y > top then
            scrollTo(offset - pageSize(scrollEntries, offset, -1))
        elseif bottom and y < bottom then
            scrollTo(offset + pageSize(scrollEntries, offset + 1, 1))
        end
    end)

    thumb = CreateFrame("Frame", nil, scrollBar)
    thumb:EnableMouse(true)
    thumb.fill = fill(thumb, C.edge, 1, "ARTWORK")
    thumb.fill:SetPoint("TOP")
    thumb.fill:SetPoint("BOTTOM")

    local function shade(color)
        thumb.fill:SetColorTexture(color[1], color[2], color[3], 1)
    end
    thumb:SetScript("OnEnter", function() shade(C.faint) end)
    thumb:SetScript("OnLeave", function() if not thumb.dragging then shade(C.edge) end end)
    thumb:SetScript("OnMouseDown", function(_, button)
        if button ~= "LeftButton" then return end
        thumb.dragging = { y = cursorY(thumb), offset = offset }
        thumb:SetScript("OnUpdate", function()
            if scrollRoom <= 0 then return end
            local moved = thumb.dragging.y - cursorY(thumb)
            scrollTo(thumb.dragging.offset + math.floor(moved / scrollRoom * maxScroll + 0.5))
        end)
    end)
    thumb:SetScript("OnMouseUp", function()
        thumb.dragging = nil
        thumb:SetScript("OnUpdate", nil)
        if not thumb:IsMouseOver() then shade(C.edge) end
    end)
    scrollBar:Hide()
end

local function drawScrollBar(entries)
    scrollEntries = entries
    maxScroll = maxOffset(entries)
    if maxScroll == 0 then
        scrollRoom = 0
        scrollBar:Hide()
        return
    end
    local total = 0
    for _, item in ipairs(entries) do total = total + entryHeight(item) end
    local track = R.listH - 4
    local height = math.max(M.THUMB_MIN, math.floor(track * R.listH / total))
    scrollRoom = track - height
    thumb:SetHeight(height)
    thumb:ClearAllPoints()
    thumb:SetPoint("TOP", scrollBar, "TOP", 0, -math.floor(scrollRoom * offset / maxScroll + 0.5))
    scrollBar:Show()
end

local function createGrip()
    R.grip = CreateFrame("Button", nil, frame)
    R.grip:SetFrameLevel(frame:GetFrameLevel() + 6)
    R.grip:SetPoint("BOTTOMRIGHT", -1, 1)
    R.grip:SetNormalTexture("Interface\\ChatFrame\\UI-ChatIM-SizeGrabber-Up")
    R.grip:SetHighlightTexture("Interface\\ChatFrame\\UI-ChatIM-SizeGrabber-Highlight")
    R.grip:SetPushedTexture("Interface\\ChatFrame\\UI-ChatIM-SizeGrabber-Down")
    R.grip:SetScript("OnMouseDown", function(_, button)
        if button ~= "LeftButton" then return end
        remember()
        place()
        local minW, minH = minSize()
        T.BeginSizing(frame, minW, minH, UIParent:GetWidth() or 4096, UIParent:GetHeight() or 4096)
    end)
    R.grip:SetScript("OnMouseUp", function()
        T.EndSizing(frame)
        Window:KeepSize()
        remember()
        place()
    end)
end

local function applyBounds()
    local minW, minH = minSize()
    local maxW = UIParent.GetWidth and UIParent:GetWidth() or 4096
    local maxH = UIParent.GetHeight and UIParent:GetHeight() or 4096
    if frame.SetResizeBounds then
        frame:SetResizeBounds(minW, minH, math.max(minW, maxW), math.max(minH, maxH))
    elseif frame.SetMinResize then
        frame:SetMinResize(minW, minH)
    end
end

local function arrange()
    R.listTop = M.HEADER + M.INFO + M.TABS_GAP * 2 + M.TABS_H + M.HEAD_H
    R.listH = math.max(M.ROW_H, dims.h - R.listTop - M.FOOTER_H)

    R.headerBar:SetHeight(M.HEADER - 1)
    R.drag:SetHeight(M.HEADER)
    R.icon:SetSize(M.ICON, M.ICON)
    R.icon:ClearAllPoints()
    R.icon:SetPoint("TOPLEFT", M.PAD, -math.floor((M.HEADER - M.ICON) / 2))
    optionsButton:SetSize(math.max(optionsButton.label:GetStringWidth(), 10) + 8, M.OPTIONS_H)
    optionsButton:ClearAllPoints()
    optionsButton:SetPoint("TOPRIGHT", -28, -math.floor((M.HEADER - M.OPTIONS_H) / 2))

    R.infoBar:ClearAllPoints()
    R.infoBar:SetPoint("TOPLEFT", 1, -M.HEADER)
    R.infoBar:SetPoint("TOPRIGHT", -1, -M.HEADER)
    R.infoBar:SetHeight(M.INFO)
    setRule(R.infoRule, M.HEADER + M.INFO - 1)
    info.reset:ClearAllPoints()
    info.reset:SetPoint("RIGHT", R.infoBar, "RIGHT", -M.PAD, 0)
    info.affixLabel:ClearAllPoints()
    info.affixLabel:SetPoint("LEFT", R.infoBar, "LEFT", M.PAD, 0)

    R.tabBar:ClearAllPoints()
    R.tabBar:SetPoint("TOPLEFT", 1, -(M.HEADER + M.INFO))
    R.tabBar:SetPoint("TOPRIGHT", -1, -(M.HEADER + M.INFO))
    R.tabBar:SetHeight(M.TABS_GAP * 2 + M.TABS_H)
    setRule(R.tabsRule, M.HEADER + M.INFO + M.TABS_GAP * 2 + M.TABS_H - 1)
    for i, name in ipairs(TABS) do
        local button = tabButtons[name]
        button:SetSize(M.TAB_W, M.TABS_H)
        button:ClearAllPoints()
        button:SetPoint("TOPLEFT", M.PAD + (i - 1) * M.TAB_W, -(M.HEADER + M.INFO + M.TABS_GAP))
    end
    R.postButton:ClearAllPoints()
    R.postButton:SetPoint("RIGHT", frame, "TOPRIGHT", -M.PAD, -(M.HEADER + M.INFO + M.TABS_GAP + math.floor(M.TABS_H / 2)))

    for _, button in pairs(headings) do button:SetHeight(M.HEAD_H) end
    setRule(R.headRule, R.listTop - 1)
    sortArrow:SetSize(M.ARROW, M.ARROW)

    list:ClearAllPoints()
    list:SetPoint("TOPLEFT", 1, -R.listTop)
    list:SetSize(dims.w - 2, R.listH)
    for _, entry in ipairs(entryFrames) do entry:SetWidth(dims.w - 2) end

    empty.title:ClearAllPoints()
    empty.title:SetPoint("TOP", 0, -math.max(0, math.floor(R.listH / 2) - round(34 * factor)))
    empty.title:SetWidth(math.max(0, dims.w - 80))
    empty.hint:SetWidth(M.EMPTY_HINT)
    empty.command:SetWidth(M.EMPTY_ACTION)

    scrollBar:SetWidth(M.BAR_HIT)
    thumb:SetWidth(M.BAR_HIT)
    thumb.fill:SetWidth(M.BAR_W)

    setRule(R.footerRule, dims.h - M.FOOTER_H)
    local lift = round(8 * factor)
    footer.left:ClearAllPoints()
    footer.left:SetPoint("BOTTOMLEFT", M.PAD, lift)
    footer.right:ClearAllPoints()
    footer.right:SetPoint("BOTTOMRIGHT", -(M.PAD + M.GRIP), lift)

    R.grip:SetSize(M.GRIP, M.GRIP)
end

local function applySize()
    frame:SetSize(dims.w, dims.h)
    applyBounds()
    arrange()
    Window:Refresh()
end

local function create()
    measure()
    frame = CreateFrame("Frame", "RaidexKeysWindow", UIParent)
    frame:SetFrameStrata("HIGH")
    frame:SetToplevel(true)
    frame:SetClampedToScreen(true)
    frame:SetMovable(true)
    frame:SetResizable(true)
    frame:EnableMouse(true)
    frame:EnableMouseWheel(true)
    frame:SetScript("OnMouseWheel", function(_, delta)
        local view = Window.view or Window.View()
        local rows = Window.Rows(view, tab)
        local entries = Window.Entries(rows, tab, sorts[tab])
        offset = math.min(maxOffset(entries), math.max(0, offset - delta))
        Window:Refresh()
    end)
    frame:SetScript("OnSizeChanged", function(_, width, height)
        width, height = round(width), round(height)
        if width == dims.w and height == dims.h then return end
        dims.w, dims.h = width, height
        arrange()
        Window:Refresh()
    end)

    frame:SetScript("OnHide", function()
        if leaving then return end
        T.DB:Settings().window.open = false
        closed()
    end)
    local kept = T.DB:Settings().window.tab
    for _, name in ipairs(TABS) do
        if name == kept then tab = name end
    end

    local background = fill(frame, C.bg, 0.95)
    background:SetAllPoints()
    edges(frame)

    createHeader()
    createInfo()
    createTabs()
    createHeadings()
    createList()
    createScrollBar()
    createFooter()
    createGrip()
    frame:SetSize(dims.w, dims.h)
    applyBounds()
    arrange()
    place()
    frame:Hide()

    if UISpecialFrames then UISpecialFrames[#UISpecialFrames + 1] = "RaidexKeysWindow" end
end

local function tabLabel(name)
    if name == "chars" then return L["Alts"] end
    if name == "party" then return L["Group"] end
    if name == "goal" then return L["Rating goals"] end
    if name == "board" then return L["Board"] end
    if name == "raids" then return L["Raids"] end
    return L["Guild"]
end

local HEADING = {
    name = "Character", dungeon = "Dungeon", key = "Key",
    week = "Week", vault = "Vault", sync = "Updated", rating = "Rating", difficulty = "Difficulty",
}

Window.TABS = TABS
Window.DefaultSort = defaultSort
Window.TabLabel = tabLabel

function Window.Sorted(which)
    return SORTED[which] == true
end

function Window.HeadingLabel(which, col)
    local label = HEADING_FOR[which] and HEADING_FOR[which][col] or HEADING[col]
    return L[label]:upper()
end

local function drawHeadings(pos, sort, which)
    local sorted = SORTED[which] == true
    for name, button in pairs(headings) do
        local box = pos[name]
        if box then
            local label = HEADING_FOR[which] and HEADING_FOR[which][name] or HEADING[name]
            button:ClearAllPoints()
            button:SetPoint("TOPLEFT", box.x, -(R.listTop - M.HEAD_H))
            button:SetWidth(box.w)
            button.label:SetWidth(box.w)
            button.label:SetText(L[label]:upper())
            tint(button.label, sorted and sort.col == name and C.tabText or C.muted)
            button:SetEnabled(sorted and SORTABLE[name] == true)
            button:Show()
        else
            button:Hide()
        end
    end

    local box = pos[sort.col]
    if not (sorted and box) then sortArrow:Hide() return end
    sortArrow:ClearAllPoints()
    if sort.col == "rating" then
        local label = headings.rating.label
        sortArrow:SetPoint("RIGHT", label, "RIGHT", -(math.min(label:GetStringWidth(), box.w) + 3), 0)
    else
        sortArrow:SetPoint("LEFT", headings[sort.col].label, "LEFT",
            math.min(headings[sort.col].label:GetStringWidth() + 4, box.w), 0)
    end
    sortArrow:SetRotation(sort.dir == "desc" and -math.pi / 2 or math.pi / 2)
    sortArrow:Show()
end

local function drawEntries(entries, pos, sample)
    local used, shown = 0, 0
    for index = offset + 1, #entries do
        if used >= R.listH then break end
        shown = shown + 1
        local entry = entryFrames[shown]
        if not entry then
            entry = createEntry()
            entryFrames[shown] = entry
        end
        entry:ClearAllPoints()
        entry:SetPoint("TOPLEFT", 0, -used)
        local item = entries[index]
        if item.group then drawGroup(entry, item) else drawRow(entry, item, pos, sample) end
        entry:Show()
        used = used + entryHeight(item)
    end
    for i = shown + 1, #entryFrames do
        entryFrames[i].row = nil
        entryFrames[i]:Hide()
    end
end

local function drawEmpty(state)
    empty.title:SetText(state.title)
    empty.hint:SetText(state.hint)
    empty.action.opens = (state.options and "options") or (state.form and "form") or nil
    local pressed = state.action ~= nil and empty.action.opens ~= nil
    empty.action:SetLabel(state.action or "")
    empty.action:SetShown(pressed)
    empty.command:SetText(pressed and "" or state.action or "")
    empty.command:SetShown(state.action ~= nil and not pressed)
    empty:Show()
end

function Window:Refresh()
    if not frame or not frame:IsShown() then return end
    local groupOpen = preview or IsInGroup() == true
    if tab == "party" and not groupOpen then
        tab, offset = "chars", 0
        T.DB:Settings().window.tab = tab
    end
    local view = preview and T.Sample.View() or Window.View(tab)
    Window.view = view
    local rows, sample = Window.Rows(view, tab)
    local sort = sorts[tab]
    local entries = Window.Entries(rows, tab, sort)

    for _, name in ipairs(TABS) do
        local button = tabButtons[name]
        local active = name == tab
        button.label:SetText(tabLabel(name))
        button.background:SetShown(active)
        button.underline:SetShown(active)
        local enabled = name ~= "party" or groupOpen
        button:SetEnabled(enabled)
        if active or not enabled then button.hover:Hide() end
        tint(button.label, active and C.tabText or enabled and C.muted or C.dim)
    end

    info.affixes:SetText(Window.AffixText(view))
    info.reset:SetText(Window.ResetText())

    local pos = Window.Layout(Window.Columns(tab), dims.w)
    R.postButton:SetShown(tab == "board" and not sample and view.board ~= nil)

    if T.BoardPane:IsOpen() and (tab ~= "board" or sample) then
        T.BoardPane:Close()
        return
    end
    if T.RaidPane:IsOpen() and (tab ~= "raids" or sample) then
        T.RaidPane:Close()
        return
    end
    local paneOpen = T.BoardPane:IsOpen() or T.RaidPane:IsOpen()

    offset = math.max(0, math.min(offset, maxOffset(entries)))
    if paneOpen then
        for _, button in pairs(headings) do button:Hide() end
        sortArrow:Hide()
        hovered = nil
        for _, entry in ipairs(entryFrames) do entry.row = nil entry:Hide() end
        footer.hint:SetText("")
        scrollBar:Hide()
        empty:Hide()
        T.BoardPane:Refresh()
        T.RaidPane:Refresh(view.raids)
    elseif Window.IsEmpty(view, tab, entries) then
        drawHeadings(pos, sort, tab)
        hovered = nil
        for _, entry in ipairs(entryFrames) do entry.row = nil entry:Hide() end
        footer.hint:SetText("")
        scrollBar:Hide()
        drawEmpty(Window.EmptyState(view, tab))
    else
        drawHeadings(pos, sort, tab)
        empty:Hide()
        drawEntries(entries, pos, sample)
        drawScrollBar(entries)
    end

    footer.left:SetText(Window.FooterLeft(view, tab))
    footer.right:SetText(sample and L["Sample values"] or "")
end

function Window:Restyle()
    measure()
    if not frame then return end
    for region, color in pairs(textColors) do
        region:SetTextColor(color[1], color[2], color[3])
    end
    for region, entry in pairs(textureColors) do
        local color = entry.color
        region:SetColorTexture(color[1], color[2], color[3], entry.alpha)
    end
    if sortArrow then sortArrow:SetVertexColor(C.tabText[1], C.tabText[2], C.tabText[3]) end
    for _, entry in ipairs(fontStrings) do setFont(entry.fs, entry.size) end
    applySize()
end

function Window:Open(which)
    if not frame then create() end
    if which then tab, offset = which, 0 end
    local settings = T.DB:Settings().window
    settings.open, settings.tab = true, tab
    frame:Show()
    self:Refresh()
    T.Exchange:RefreshParty()
    T.Exchange:RefreshGuild()
    T.Raids:Load(tab == "raids")
    if not ticker then ticker = C_Timer.NewTicker(5, function() Window:Refresh() end) end
end

function Window:Close()
    T.DB:Settings().window.open = false
    if frame then frame:Hide() end
    closed()
end

function Window:KeepSize()
    if not frame then return end
    local settings = T.DB:Settings().window
    settings.w = (frame:GetWidth() or dims.w) / factor
    settings.h = (frame:GetHeight() or dims.h) / factor
end

function Window:ResetPosition()
    local settings = T.DB:Settings().window
    settings.x, settings.y = nil, nil
    if frame then place() end
end

function Window:Toggle()
    if frame and frame:IsShown() then self:Close() else self:Open() end
end

function Window:IsShown()
    return frame ~= nil and frame:IsShown()
end

function Window:SetPreview(on)
    preview = on
    offset = 0
    if on then self:Open() else self:Refresh() end
end

function Window:Start()
    local function restore()
        if T.DB:Settings().window.open and not self:IsShown() then self:Open() end
    end
    T.Addon:On("PLAYER_LEAVING_WORLD", function() leaving = true end)
    T.Addon:On("LOADING_SCREEN_ENABLED", function() leaving = true end)
    T.Addon:On("LOADING_SCREEN_DISABLED", function()
        leaving = false
        restore()
    end)
    T.Addon:On("PLAYER_ENTERING_WORLD", restore)
    T.Addon:On("GROUP_ROSTER_UPDATE", function() self:Refresh() end)
end
