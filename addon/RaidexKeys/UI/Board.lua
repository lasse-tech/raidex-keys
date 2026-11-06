local _, T = ...
local L = T.L

local BoardPane = {}
T.BoardPane = BoardPane

local STEP_MINUTES = 15
local ROLE_NAMES = { T = "TANK", H = "HEALER", D = "DAMAGER" }
local ROLE_FALLBACK = { T = "Tank", H = "Healer", D = "Damage" }
local WEEKDAYS = { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" }

local pane
local mode, postId
local listOffset = 0
local listRows, listTop, listLineH = 0, 0, 0
local form = { day = 0, minutes = 19 * 60 }
local W, C

local function epochDays(year, month, day)
    if month <= 2 then year = year - 1 end
    local era = math.floor(year / 400)
    local yoe = year - era * 400
    local doy = math.floor((153 * ((month + 9) % 12) + 2) / 5) + day - 1
    return era * 146097 + yoe * 365 + math.floor(yoe / 4) - math.floor(yoe / 100) + doy - 719468
end
BoardPane.EpochDays = epochDays

local function quarter(seconds)
    return math.floor(seconds / 900 + 0.5) * 900
end

local function localOffset(at)
    local t = date("*t", at)
    return epochDays(t.year, t.month, t.day) * 86400 + t.hour * 3600 + t.min * 60 + t.sec - at
end

function BoardPane.RealmOffset(at)
    local now = GetServerTime()
    local cal = C_DateAndTime and C_DateAndTime.GetCurrentCalendarTime
        and T.Plain(C_DateAndTime.GetCurrentCalendarTime())
    local offset
    if cal and cal.year and cal.hour then
        offset = quarter(epochDays(cal.year, cal.month, cal.monthDay) * 86400
            + cal.hour * 3600 + cal.minute * 60 - now)
    else
        offset = quarter(localOffset(now))
    end
    if at then offset = offset + quarter(localOffset(at) - localOffset(now)) end
    return offset
end

local function realm(at)
    return at + BoardPane.RealmOffset(at)
end

local DATE_PATTERNS = { dmy = "%d.%m.", mdy = "%m/%d", iso = "%Y-%m-%d" }

local function twelveHours()
    local choice = T.DB:Settings().window.timeFormat
    if choice == "12" then return true end
    if choice == "24" then return false end
    return GetCVarBool ~= nil and GetCVarBool("timeMgrUseMilitaryTime") == false
end

function BoardPane.ClockText(minutes)
    local hour, minute = math.floor(minutes / 60) % 24, minutes % 60
    if not twelveHours() then return ("%02d:%02d"):format(hour, minute) end
    local suffix = hour < 12 and (TIMEMANAGER_AM or "AM") or (TIMEMANAGER_PM or "PM")
    return ("%d:%02d %s"):format((hour + 11) % 12 + 1, minute, suffix)
end

function BoardPane.DayText(at)
    local shown = realm(at)
    local index = tonumber(date("!%w", shown)) + 1
    local names = CALENDAR_WEEKDAY_NAMES
    local weekday = names and names[index] or L[WEEKDAYS[index]]
    local pattern = DATE_PATTERNS[T.DB:Settings().window.dateFormat] or L["%m/%d"]
    return ("%s, %s"):format(weekday, date("!" .. pattern, shown))
end

function BoardPane.TimeText(at)
    local shown = realm(at)
    return BoardPane.ClockText(tonumber(date("!%H", shown)) * 60 + tonumber(date("!%M", shown)))
end

function BoardPane.DayKey(at)
    return date("!%Y-%m-%d", realm(at))
end

function BoardPane.RoleName(letter)
    return _G[ROLE_NAMES[letter] or "DAMAGER"] or L[ROLE_FALLBACK[letter] or "Damage"]
end

local function realmToday()
    local shown = realm(GetServerTime())
    return shown - shown % 86400
end

local function fromRealm(wall)
    return wall - BoardPane.RealmOffset(wall - BoardPane.RealmOffset())
end

function BoardPane.FormTime(day, minutes)
    return fromRealm(realmToday() + (day or 0) * 86400 + (minutes or 0) * 60)
end

function BoardPane.WallTime(year, month, day, minutes)
    return fromRealm(epochDays(year, month, day) * 86400 + (minutes or 0) * 60)
end

local function formDefaults()
    local minutes = math.floor((realm(GetServerTime() + 3600) - realmToday()) / 60)
    minutes = math.ceil(minutes / STEP_MINUTES) * STEP_MINUTES
    form.day = math.floor(minutes / (24 * 60))
    form.minutes = minutes % (24 * 60)
end

local function whisper(name)
    if name and name ~= T.Board.Me() then ChatFrameUtil.SendTell(name) end
end
BoardPane.Whisper = whisper

local function hex(color)
    return ("|cff%02x%02x%02x"):format(math.floor(color[1] * 255 + 0.5),
        math.floor(color[2] * 255 + 0.5), math.floor(color[3] * 255 + 0.5))
end

local function named(name, classFile)
    local color = W.ClassColor(nil, classFile) or C.text
    return hex(color) .. (name or "?") .. "|r"
end

local function button(label, onClick)
    return W.Button(pane, label, onClick)
end

function BoardPane:Create(frame, list)
    W, C = T.Window, T.Window.C
    pane = CreateFrame("Frame", nil, frame)
    pane:SetFrameLevel(frame:GetFrameLevel() + 3)
    pane:SetAllPoints(list)
    pane:EnableMouse(true)
    pane:EnableMouseWheel(true)
    pane:SetScript("OnMouseWheel", function(_, delta) BoardPane:Scroll(-delta) end)
    local background = W.Fill(pane, C.bg, 1)
    background:SetAllPoints()

    pane.title = W.Text(pane, 16, C.text)
    pane.sub = W.Text(pane, 12, C.muted)
    pane.note = W.Text(pane, 12, C.body)
    pane.note:SetWordWrap(true)
    pane.back = button(L["Back"], function() BoardPane:Close() end)
    pane.holder = W.Link(pane, "", function() whisper(pane.holderName) end)
    pane.message = W.Text(pane, 12, C.muted)

    pane.lines = {}
    for i = 1, T.Board.MAX_APPLICANTS + 1 do
        local line = CreateFrame("Button", nil, pane)
        line.label = W.Text(line, 12, C.body)
        line.label:SetPoint("LEFT")
        line:SetScript("OnClick", function() whisper(line.who) end)
        line:SetScript("OnEnter", function() line.label:SetAlpha(0.75) end)
        line:SetScript("OnLeave", function() line.label:SetAlpha(1) end)
        pane.lines[i] = line
    end

    pane.roles = {}
    for _, letter in ipairs({ "T", "H", "D" }) do
        pane.roles[letter] = button("", function()
            if not T.Board:SignUp(postId, letter) then
                pane.message:SetText(L["The key is full."])
            end
        end)
    end
    pane.bar = CreateFrame("Frame", nil, pane)
    pane.bar:SetFrameLevel(pane:GetFrameLevel() + 2)
    pane.bar:EnableMouse(true)
    pane.bar:SetScript("OnMouseDown", function(_, click)
        if click ~= "LeftButton" then return end
        local y, top, bottom = W.CursorY(pane.bar), pane.thumb:GetTop(), pane.thumb:GetBottom()
        if type(y) ~= "number" then return end
        if type(top) == "number" and y > top then BoardPane:Scroll(-listRows)
        elseif type(bottom) == "number" and y < bottom then BoardPane:Scroll(listRows) end
    end)

    pane.thumb = CreateFrame("Frame", nil, pane.bar)
    pane.thumb:EnableMouse(true)
    pane.thumb.fill = W.Fill(pane.thumb, C.edge, 1, "ARTWORK")
    pane.thumb.fill:SetAllPoints()
    local function shade(color) pane.thumb.fill:SetColorTexture(color[1], color[2], color[3], 1) end
    pane.thumb:SetScript("OnEnter", function() shade(C.faint) end)
    pane.thumb:SetScript("OnLeave", function() if not pane.thumb.dragging then shade(C.edge) end end)
    pane.thumb:SetScript("OnMouseDown", function(_, click)
        if click ~= "LeftButton" then return end
        pane.thumb.dragging = { y = W.CursorY(pane.thumb), offset = listOffset }
        pane.thumb:SetScript("OnUpdate", function()
            local room, most = pane.barRoom or 0, math.max(0, (pane.listCount or 0) - listRows)
            local y = W.CursorY(pane.thumb)
            if room <= 0 or most <= 0 or type(y) ~= "number" then return end
            BoardPane:ScrollTo(pane.thumb.dragging.offset + math.floor((pane.thumb.dragging.y - y) / room * most + 0.5))
        end)
    end)
    pane.thumb:SetScript("OnMouseUp", function()
        pane.thumb.dragging = nil
        pane.thumb:SetScript("OnUpdate", nil)
        if not pane.thumb:IsMouseOver() then shade(C.edge) end
    end)
    pane.bar:Hide()

    pane.withdraw = button(L["Take back sign-up"], function() T.Board:Withdraw(postId) end)
    pane.remove = button(L["Take posting down"], function()
        T.Board:Remove(postId)
        BoardPane:Close()
    end)

    pane.dayLabel = W.Text(pane, 12, C.text, "CENTER")
    pane.dayPrev = button("<", function() form.day = math.max(0, form.day - 1) BoardPane:Refresh() end)
    pane.dayNext = button(">", function() form.day = math.min(T.Raids.DAYS_AHEAD, form.day + 1) BoardPane:Refresh() end)
    pane.timeLabel = W.Text(pane, 12, C.text, "CENTER")
    pane.timePrev = button("<", function()
        form.minutes = (form.minutes - STEP_MINUTES) % (24 * 60)
        BoardPane:Refresh()
    end)
    pane.timeNext = button(">", function()
        form.minutes = (form.minutes + STEP_MINUTES) % (24 * 60)
        BoardPane:Refresh()
    end)
    pane.noteLabel = W.Text(pane, 11, C.muted)
    pane.noteBox = CreateFrame("EditBox", nil, pane, "InputBoxTemplate")
    pane.noteBox:SetAutoFocus(false)
    if pane.noteBox.SetMaxBytes then pane.noteBox:SetMaxBytes(T.Board.NOTE_MAX + 1) end
    pane.noteBox:SetScript("OnEscapePressed", function(box) box:ClearFocus() end)
    pane.noteBox:SetScript("OnEnterPressed", function() BoardPane:Submit() end)
    pane.submit = button(L["Post"], function() BoardPane:Submit() end)
    pane.cancel = button(L["Cancel"], function() BoardPane:Close() end)

    pane:Hide()
end

local function listGeometry()
    local M, factor = W.Metrics()
    local function gap(value) return math.floor(value * factor + 0.5) end
    local function heightOf(region)
        local height = region.GetStringHeight and region:GetStringHeight()
        return type(height) == "number" and height or 0
    end
    local lineH = gap(18)
    local top = gap(12) + heightOf(pane.title) + gap(6) + heightOf(pane.sub)
        + gap(8) + heightOf(pane.note) + gap(12)
    local foot = M.OPTIONS_H + gap(6) + gap(10) + gap(8)
    local room = (W.ListHeight() or 0) - top - foot
    return top, lineH, math.max(1, math.floor(room / lineH))
end

function BoardPane:Arrange()
    if not pane then return end
    local M, factor = W.Metrics()
    local function gap(value) return math.floor(value * factor + 0.5) end
    local pad = M.PAD

    pane.back:ClearAllPoints()
    pane.back:SetPoint("TOPRIGHT", -pad, -gap(10))
    pane.title:ClearAllPoints()
    pane.title:SetPoint("TOPLEFT", pad, -gap(12))
    pane.title:SetPoint("RIGHT", pane.back, "LEFT", -gap(8), 0)
    pane.sub:ClearAllPoints()
    pane.sub:SetPoint("TOPLEFT", pane.title, "BOTTOMLEFT", 0, -gap(6))

    if mode == "form" then
        pane.dayPrev:ClearAllPoints()
        pane.dayPrev:SetPoint("TOPLEFT", pane.sub, "BOTTOMLEFT", 0, -gap(16))
        pane.dayLabel:ClearAllPoints()
        pane.dayLabel:SetPoint("LEFT", pane.dayPrev, "RIGHT", gap(6), 0)
        pane.dayLabel:SetWidth(gap(170))
        pane.dayNext:ClearAllPoints()
        pane.dayNext:SetPoint("LEFT", pane.dayLabel, "RIGHT", gap(6), 0)
        pane.timePrev:ClearAllPoints()
        pane.timePrev:SetPoint("LEFT", pane.dayNext, "RIGHT", gap(24), 0)
        pane.timeLabel:ClearAllPoints()
        pane.timeLabel:SetPoint("LEFT", pane.timePrev, "RIGHT", gap(6), 0)
        pane.timeLabel:SetWidth(gap(72))
        pane.timeNext:ClearAllPoints()
        pane.timeNext:SetPoint("LEFT", pane.timeLabel, "RIGHT", gap(6), 0)
        pane.noteLabel:ClearAllPoints()
        pane.noteLabel:SetPoint("TOPLEFT", pane.dayPrev, "BOTTOMLEFT", 0, -gap(16))
        pane.noteBox:ClearAllPoints()
        pane.noteBox:SetPoint("TOPLEFT", pane.noteLabel, "BOTTOMLEFT", gap(6), -gap(4))
        pane.noteBox:SetPoint("RIGHT", pane, "RIGHT", -pad, 0)
        pane.noteBox:SetHeight(gap(24))
        pane.submit:ClearAllPoints()
        pane.submit:SetPoint("TOPLEFT", pane.noteBox, "BOTTOMLEFT", -gap(6), -gap(14))
        pane.cancel:ClearAllPoints()
        pane.cancel:SetPoint("LEFT", pane.submit, "RIGHT", gap(16), 0)
        pane.message:ClearAllPoints()
        pane.message:SetPoint("TOPLEFT", pane.submit, "BOTTOMLEFT", 0, -gap(10))
        pane.message:SetPoint("RIGHT", pane, "RIGHT", -pad, 0)
        return
    end

    pane.holder:ClearAllPoints()
    pane.holder:SetPoint("LEFT", pane.sub, "RIGHT", gap(6), 0)
    pane.note:ClearAllPoints()
    pane.note:SetPoint("TOPLEFT", pane.sub, "BOTTOMLEFT", 0, -gap(8))
    pane.note:SetPoint("RIGHT", pane, "RIGHT", -pad, 0)
    local lineH = listLineH > 0 and listLineH or select(2, listGeometry())
    for i, line in ipairs(pane.lines) do
        line:ClearAllPoints()
        line:SetPoint("TOPLEFT", pane.note, "BOTTOMLEFT", 0, -gap(12) - (i - 1) * lineH)
        line:SetPoint("RIGHT", pane, "RIGHT", -pad, 0)
        line:SetHeight(lineH)
    end
    local listH = listRows * lineH
    pane.bar:ClearAllPoints()
    pane.bar:SetPoint("TOPRIGHT", pane, "TOPRIGHT", -3, -listTop)
    pane.bar:SetSize(M.BAR_W, math.max(lineH, listH))
    local most = math.max(0, (pane.listCount or 0) - listRows)
    if most == 0 then
        pane.barRoom = 0
        pane.bar:Hide()
    else
        local height = math.max(M.THUMB_MIN, math.floor(listH * listRows / (pane.listCount or 1)))
        pane.barRoom = listH - height
        pane.thumb:ClearAllPoints()
        pane.thumb:SetSize(M.BAR_W, height)
        pane.thumb:SetPoint("TOP", pane.bar, "TOP", 0, -math.floor(pane.barRoom * listOffset / most + 0.5))
        pane.bar:Show()
    end

    local previous
    local function place(widget, air)
        if not widget:IsShown() then return end
        widget:ClearAllPoints()
        if previous then widget:SetPoint("LEFT", previous, "RIGHT", gap(air or 16), 0)
        else widget:SetPoint("BOTTOMLEFT", pad, gap(10)) end
        previous = widget
    end
    for _, name in ipairs({ "T", "H", "D" }) do place(pane.roles[name]) end
    place(pane.withdraw, 28)
    place(pane.remove, 28)
    pane.message:ClearAllPoints()
    pane.message:SetPoint("BOTTOMRIGHT", -pad, gap(12))
end

local POST_PARTS = { "holder", "note", "withdraw", "remove", "bar" }
local FORM_PARTS = { "dayLabel", "dayPrev", "dayNext", "timeLabel", "timePrev", "timeNext",
    "noteLabel", "noteBox", "submit", "cancel" }

local function showParts(parts, on)
    for _, name in ipairs(parts) do pane[name]:SetShown(on) end
end

local function keyColor()
    local glow = C.glow
    return ("|cff%02x%02x%02x"):format(math.floor(glow[1] * 255 + 0.5), math.floor(glow[2] * 255 + 0.5),
        math.floor(glow[3] * 255 + 0.5))
end

local function drawPost()
    local post = T.Board:Find(postId)
    if not post then
        BoardPane:Close()
        return
    end
    showParts(FORM_PARTS, false)
    pane.title:SetText(("%s+%d|r %s"):format(keyColor(), post.level or 0, W.MapName(post.mapId) or "?"))
    pane.sub:SetText(("%s · %s · %s"):format(BoardPane.DayText(post.at), BoardPane.TimeText(post.at), L["Key holder:"]))
    pane.holderName = post.holder
    pane.holder:SetLabel(post.role and ("%s · %s"):format(named(post.name, post.classFile),
        BoardPane.RoleName(post.role)) or named(post.name, post.classFile))
    pane.holder:Show()
    pane.note:SetText(post.note ~= "" and post.note or L["No note."])
    pane.note:Show()

    local members = { { who = post.holder, name = post.name, classFile = post.classFile,
        role = post.role, rating = post.rating } }
    for _, s in ipairs(post.applicants) do
        members[#members + 1] = { who = s.who, name = T.Exchange.ShortName(s.who),
            classFile = s.classFile, role = s.role, rating = s.rating }
    end

    local rows
    listTop, listLineH, rows = listGeometry()
    listRows = math.min(rows, #pane.lines)
    listOffset = math.max(0, math.min(listOffset, #members + (#post.applicants == 0 and 1 or 0) - listRows))

    for i, line in ipairs(pane.lines) do
        local member = members[listOffset + i]
        if member and i <= listRows then
            line.who = member.who
            local parts = { named(member.name, member.classFile) }
            parts[#parts + 1] = member.role and BoardPane.RoleName(member.role) or L["no role yet"]
            if member.rating then parts[#parts + 1] = tostring(member.rating) end
            line.label:SetText(("%d. %s"):format(listOffset + i, table.concat(parts, " · ")))
            line:Show()
        elseif i <= listRows and listOffset + i == #members + 1 and #post.applicants == 0 then
            line.who = nil
            line.label:SetText(L["Nobody has signed up yet."])
            line:Show()
        else
            line.who = nil
            line:Hide()
        end
    end
    pane.listCount = #members + (#post.applicants == 0 and 1 or 0)

    local full = #post.applicants >= T.Board.MAX_APPLICANTS
    local canSign = not post.own and not post.signedUp and not full
    for letter, role in pairs(pane.roles) do
        role:SetLabel(post.own and L["Role: %s"]:format(BoardPane.RoleName(letter))
            or L["Sign up as %s"]:format(BoardPane.RoleName(letter)))
        role:SetShown(canSign or post.own)
        role:SetEnabled(not post.own or post.role ~= letter)
    end
    pane.withdraw:SetShown(post.signedUp)
    pane.remove:SetShown(post.own)
    pane.withdraw:SetLabel(L["Take back sign-up"])
    pane.remove:SetLabel(L["Take posting down"])

    local message = T.Board.CountText(#post.applicants)
    if full and not post.own and not post.signedUp then
        message = L["Full - whisper %s to talk it over."]:format(post.name or "?")
    end
    pane.message:SetText(message)
end

local function drawForm()
    showParts(POST_PARTS, false)
    for _, line in ipairs(pane.lines) do line:Hide() end
    for _, role in pairs(pane.roles) do role:Hide() end
    showParts(FORM_PARTS, true)

    local mapId, level = T.Snapshot:OwnKey()
    pane.title:SetText(mapId and ("%s+%d|r %s"):format(keyColor(), level, W.MapName(mapId) or "?") or L["No keystone in your bags."])
    pane.sub:SetText(L["Pin your key for a day and a time."])
    pane.dayLabel:SetText(BoardPane.DayText(BoardPane.FormTime(form.day, 0)))
    pane.timeLabel:SetText(BoardPane.ClockText(form.minutes))
    pane.noteLabel:SetText(L["Note, e.g. \"a few more keys afterwards\""])
    for _, name in ipairs({ "dayPrev", "timePrev" }) do pane[name]:SetLabel("<") end
    for _, name in ipairs({ "dayNext", "timeNext" }) do pane[name]:SetLabel(">") end
    pane.submit:SetLabel(L["Post"])
    pane.cancel:SetLabel(L["Cancel"])
    pane.submit:SetEnabled(mapId ~= nil)
end

function BoardPane:Refresh()
    if not pane then return end
    if not mode then
        pane:Hide()
        return
    end
    pane.back:SetLabel(L["Back"])
    if mode == "form" then drawForm() else drawPost() end
    if mode then
        self:Arrange()
        pane:Show()
    end
end

function BoardPane:Scroll(by)
    self:ScrollTo(listOffset + (by or 0))
end

function BoardPane:ScrollTo(to)
    local most = math.max(0, ((pane and pane.listCount) or 0) - listRows)
    to = math.max(0, math.min(to or 0, most))
    if to == listOffset then return end
    listOffset = to
    self:Refresh()
end

function BoardPane:IsOpen()
    return mode ~= nil
end

function BoardPane:Open(id)
    mode, postId = "post", id
    listOffset = 0
    if pane then pane.message:SetText("") end
    T.Window:Refresh()
end

function BoardPane:OpenForm()
    mode, postId = "form", nil
    formDefaults()
    if pane then
        pane.noteBox:SetText("")
        pane.message:SetText("")
    end
    T.Window:Refresh()
end

function BoardPane:Close()
    mode, postId = nil, nil
    if pane then
        pane.noteBox:ClearFocus()
        pane:Hide()
    end
    if T.Window:IsShown() then T.Window:Refresh() end
end

function BoardPane:Submit()
    local at = BoardPane.FormTime(form.day, form.minutes)
    if at <= GetServerTime() then
        pane.message:SetText(L["That time has already passed."])
        return
    end
    local post = T.Board:Post(at, pane.noteBox:GetText())
    if not post then
        pane.message:SetText(L["No keystone in your bags."])
        return
    end
    pane.noteBox:ClearFocus()
    self:Open(post.id)
end
