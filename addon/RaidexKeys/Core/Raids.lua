local _, T = ...
local Addon, Plain = T.Addon, T.Plain

local Raids = {}
T.Raids = Raids

local PREFIX = "RaidexRC"
Raids.DAYS_AHEAD = 13
Raids.KEEP_AFTER = 3 * 3600
local KEEP_UNKNOWN = 14 * 86400

local LOAD_AFTER_LOGIN = 5
local DIGEST_AFTER_LOGIN = 25
local DIGEST_EVERY = 900
local REPLY_MIN, REPLY_SPREAD = 1, 4
local BUNDLE_TIMEOUT = 8
local MESSAGE_MAX = 240
local PER_DIGEST = 6
local INVITES_TIMEOUT = 4
local INVITES_FRESH = 60
local READ_AFTER_ANSWER = 2
local ROLE_ORDER = { T = 1, H = 2, D = 3 }

local B36, Num36, Hash = T.Board.B36, T.Board.Num36, T.Board.Hash

local function me() return T.Board.Me() end

local function takesPart(status)
    local S = Enum and Enum.CalendarStatus or {}
    return status ~= nil and (status == (S.Signedup or 6) or status == (S.Confirmed or 3)
        or status == (S.Available or 1) or status == (S.Standby or 5) or status == (S.Tentative or 8))
end

local function ownItemLevel()
    if not GetAverageItemLevel then return nil end
    local _, equipped = GetAverageItemLevel()
    equipped = Plain(equipped)
    return equipped and equipped > 0 and math.floor(equipped) or nil
end

local function store()
    if not IsInGuild() then return nil end
    local name, _, _, realm = GetGuildInfo("player")
    name = Plain(name)
    if not name then return nil end
    local key = name .. "-" .. (realm or GetRealmName())
    local all = T.DB:Data().raids
    all[key] = all[key] or { events = {} }
    return all[key]
end

local function record(guild, key)
    local event = guild.events[key]
    if not event then
        event = { signups = {} }
        guild.events[key] = event
    end
    return event
end

Raids.NOTE_MAX = T.Board.NOTE_MAX

function Raids.CleanNote(note)
    return (T.Board.CleanNote((note or ""):gsub("[;\r\n]", " ")))
end

local function oneLine(text)
    return ((text or ""):gsub("[\r\n]+", " "):gsub("^%s+", ""):gsub("%s+$", ""))
end

local function entryText(s)
    return table.concat({ s.who, s.role or "D", s.classFile or "", B36(s.itemLevel), B36(s.signedAt), B36(s.ver),
        s.removed and "1" or "0", s.assigned and "1" or "0", s.note or "" }, ",")
end

local function parseEntry(text)
    local who, role, classFile, itemLevel, signedAt, ver, removed, assigned, note =
        text:match("^([^,]+),([THD]),([^,]*),(%w+),(%w+),(%w+),([01]),([01]),?(.*)$")
    if not who then return nil end
    itemLevel = Num36(itemLevel)
    note = Raids.CleanNote(note)
    return { who = who, role = role, classFile = classFile ~= "" and classFile or nil,
        itemLevel = itemLevel and itemLevel > 0 and itemLevel or nil,
        signedAt = Num36(signedAt), ver = Num36(ver), removed = removed == "1", assigned = assigned == "1",
        note = note ~= "" and note or nil }
end

local function sortedWhos(event)
    local whos = {}
    for who in pairs(event.signups) do whos[#whos + 1] = who end
    table.sort(whos)
    return whos
end

local function eventHash(event)
    local parts = {}
    for _, who in ipairs(sortedWhos(event)) do parts[#parts + 1] = entryText(event.signups[who]) end
    return Hash(table.concat(parts, ";"))
end

local function newer(a, b)
    if (a.ver or 0) ~= (b.ver or 0) then return (a.ver or 0) > (b.ver or 0) end
    return entryText(a) > entryText(b)
end

local function send(message)
    T.Board.Send(PREFIX, message)
end

local function sendEntry(key, entry)
    send("U~" .. key .. "~" .. entryText(entry))
end

local function sendBundle(key, event)
    local whos = sortedWhos(event)
    send(("B~%s~%s~%s~%d"):format(key, eventHash(event), B36(event.at), #whos))
    local head, entries, size = "S~" .. key .. "~", {}, 0
    for _, who in ipairs(whos) do
        local text = entryText(event.signups[who])
        if #entries > 0 and #head + size + 1 + #text > MESSAGE_MAX then
            send(head .. table.concat(entries, ";"))
            entries, size = {}, 0
        end
        size = size + #text + (#entries > 0 and 1 or 0)
        entries[#entries + 1] = text
    end
    if #entries > 0 then send(head .. table.concat(entries, ";")) end
end

local function expired(event, now)
    now = now or GetServerTime()
    if event.at then return event.at + Raids.KEEP_AFTER < now end
    local last = 0
    for _, s in pairs(event.signups) do last = math.max(last, s.ver or 0) end
    return last + KEEP_UNKNOWN < now
end

local function purge()
    for _, guild in pairs(T.DB:Data().raids) do
        for key, event in pairs(guild.events) do
            if expired(event) then guild.events[key] = nil end
        end
    end
end

local function changed()
    Addon:Debounce("raids", 0.5, function()
        if T.Window then T.Window:Refresh() end
    end)
end

local replies, heard = {}, {}

local function scheduleReply(key)
    if replies[key] then return end
    local since = GetTime()
    replies[key] = since
    C_Timer.After(REPLY_MIN + math.random() * REPLY_SPREAD, function()
        replies[key] = nil
        local guild = store()
        local event = guild and guild.events[key]
        if not event or expired(event) then return end
        local said = heard[key]
        if said and said.at >= since and said.hash == eventHash(event) then return end
        sendBundle(key, event)
    end)
end

local bundles = {}

local function finishBundle(bundleKey)
    local bundle = bundles[bundleKey]
    if not bundle then return end
    bundles[bundleKey] = nil
    local guild = store()
    local event = guild and guild.events[bundle.key]
    if event and eventHash(event) ~= bundle.hash then scheduleReply(bundle.key) end
end

local digests = {}

local function onDigest(from, last, list)
    local seen = digests[from] or {}
    digests[from] = seen
    for key, h in (list or ""):gmatch("([%w%-]+):(%x+)") do seen[key] = h end
    if last ~= "1" then return end
    digests[from] = nil
    local guild = store()
    if not guild then return end
    for key, event in pairs(guild.events) do
        if next(event.signups) and not expired(event) and seen[key] ~= eventHash(event) then
            scheduleReply(key)
        end
    end
end

local function onMessage(_, prefix, message, channel, from)
    if prefix ~= PREFIX or channel ~= "GUILD" then return end
    message, from = Plain(message), T.Board.FullName(Plain(from))
    if not (message and from) or from == me() then return end
    T.Exchange:HeardFrom(from)
    local guild = store()
    if not guild then return end

    local kind = message:sub(1, 1)
    if kind == "B" then
        local key, h, at, count = message:match("^B~([%w%-]+)~(%x+)~(%w+)~(%d+)$")
        if not key then return end
        local event = record(guild, key)
        at = Num36(at)
        if at and at > 0 and not event.at then event.at = at end
        if expired(event) then
            guild.events[key] = nil
            return
        end
        heard[key] = { hash = h, at = GetTime() }
        local bundleKey = from .. "~" .. key
        bundles[bundleKey] = { key = key, hash = h, expected = tonumber(count), got = 0 }
        if bundles[bundleKey].expected == 0 then
            finishBundle(bundleKey)
        else
            C_Timer.After(BUNDLE_TIMEOUT, function() finishBundle(bundleKey) end)
        end
    elseif kind == "S" or kind == "U" then
        local key, list = message:match("^[SU]~([%w%-]+)~(.+)$")
        if not key then return end
        local event = record(guild, key)
        local bundleKey = from .. "~" .. key
        local bundle = kind == "S" and bundles[bundleKey]
        for text in list:gmatch("[^;]+") do
            local entry = parseEntry(text)
            if entry then
                local own = event.signups[entry.who]
                if not own or newer(entry, own) then
                    event.signups[entry.who] = entry
                    changed()
                end
                if bundle then bundle.got = bundle.got + 1 end
            end
        end
        if bundle and bundle.got >= bundle.expected then finishBundle(bundleKey) end
    elseif kind == "D" then
        local last, list = message:match("^D~([01])~(.*)$")
        if last then onDigest(from, last, list) end
    end
end

local function refreshItemLevel(guild)
    local level, player = ownItemLevel(), me()
    if not level then return end
    for key, event in pairs(guild.events) do
        local mine = event.signups[player]
        if mine and not mine.removed and not mine.assigned and mine.itemLevel ~= level and not expired(event) then
            mine.itemLevel = level
            mine.ver = math.max(GetServerTime(), (mine.ver or 0) + 1)
            sendEntry(key, mine)
        end
    end
end

function Raids:SendDigest()
    local guild = store()
    if not guild then return end
    purge()
    refreshItemLevel(guild)
    local items = {}
    for key, event in pairs(guild.events) do
        if next(event.signups) then items[#items + 1] = key .. ":" .. eventHash(event) end
    end
    table.sort(items)
    local part = {}
    local function flush(last)
        send(("D~%s~%s"):format(last and "1" or "0", table.concat(part, ",")))
        part = {}
    end
    for _, item in ipairs(items) do
        part[#part + 1] = item
        if #part >= PER_DIGEST then flush(false) end
    end
    flush(true)
end

local raids = {}

local function raidKey(id, creator, title, year, month, day)
    local text = type(id) == "number" and ("%.0f"):format(id) or (id ~= nil and tostring(id)) or ""
    if not text:match("^%d+$") then text = "h" .. Hash((creator or "") .. "|" .. (title or "")) end
    return ("%s-%04d%02d%02d"):format(text, year, month, day)
end

local function isGuildRaid(kind, eventType, sequence)
    local raid = Enum and Enum.CalendarEventType and Enum.CalendarEventType.Raid or 0
    return kind == "GUILD_EVENT" and eventType == raid and sequence ~= "ONGOING" and sequence ~= "END"
end

local function instanceText(name, difficultyName)
    if not name or name == "" then return nil end
    if not difficultyName or difficultyName == "" then return name end
    return (DUNGEON_NAME_WITH_DIFFICULTY or "%s (%s)"):format(name, difficultyName)
end

local function difficultyOf(difficultyId, named)
    if named and named ~= "" then return named end
    return difficultyId and GetDifficultyInfo and Plain((GetDifficultyInfo(difficultyId))) or nil
end

local function listedInstance(event)
    local icon = Plain(event.iconTexture)
    local raidType = Enum and Enum.CalendarEventType and Enum.CalendarEventType.Raid or 0
    local textures = icon and C_Calendar.EventGetTextures and C_Calendar.EventGetTextures(raidType)
    local difficulty, named = Plain(event.difficulty), Plain(event.difficultyName)
    local found, difficulties, exact = nil, {}, nil
    for _, texture in ipairs(textures or {}) do
        if Plain(texture.iconTexture) == icon then
            local id = Plain(texture.difficultyId)
            found = found or texture
            if id then difficulties[id] = true end
            if id and id == difficulty then exact = texture end
        end
    end
    if not found then return nil end
    found = exact or found
    local single = next(difficulties) == nil or next(difficulties, next(difficulties)) == nil
    local id = (exact or single) and Plain(found.difficultyId) or nil
    return Plain(found.title), difficultyOf(id, named), Plain(found.mapId), id
end

local function openedInstance(info)
    local index = Plain(info.textureIndex)
    local textures = index and C_Calendar.EventGetTextures and C_Calendar.EventGetTextures(Plain(info.eventType))
    local texture = textures and textures[index]
    if not texture then return nil end
    local id = Plain(texture.difficultyId)
    return Plain(texture.title), difficultyOf(id), Plain(texture.mapId), id
end

local function locate(raid)
    local shown = C_Calendar.GetMonthInfo(0)
    local shownYear, shownMonth = Plain(shown and shown.year), Plain(shown and shown.month)
    if not (shownYear and shownMonth) then return nil end
    local offset = (raid.year * 12 + raid.month) - (shownYear * 12 + shownMonth)
    local ok, count = pcall(C_Calendar.GetNumDayEvents, offset, raid.day)
    for index = 1, (ok and Plain(count)) or 0 do
        local event = C_Calendar.GetDayEvent(offset, raid.day, index)
        local title = event and Plain(event.title)
        if title and raidKey(Plain(event.eventID), Plain(event.invitedBy), title,
            raid.year, raid.month, raid.day) == raid.key then
            return offset, raid.day, index, event
        end
    end
    return nil
end

local openNext

function Raids:Read()
    if not (C_Calendar and C_Calendar.GetMonthInfo and C_Calendar.GetDayEvent) then return end
    local today = C_DateAndTime and C_DateAndTime.GetCurrentCalendarTime and C_DateAndTime.GetCurrentCalendarTime()
    local shown = C_Calendar.GetMonthInfo(0)
    local year, month, day = Plain(today and today.year), Plain(today and today.month), Plain(today and today.monthDay)
    local shownYear, shownMonth = Plain(shown and shown.year), Plain(shown and shown.month)
    local windowShown = CalendarFrame ~= nil and CalendarFrame:IsShown() == true
    if year and month and not windowShown and C_Calendar.SetAbsMonth
        and (shownYear ~= year or shownMonth ~= month) then
        C_Calendar.SetAbsMonth(month, year)
        shown = C_Calendar.GetMonthInfo(0)
        shownYear, shownMonth = Plain(shown and shown.year), Plain(shown and shown.month)
    end
    if not (year and month and day and shownYear and shownMonth) then return end

    local now, found, guild, seen = GetServerTime(), {}, store(), 0
    local first = T.BoardPane.EpochDays(year, month, day)
    for offsetDays = -1, Raids.DAYS_AHEAD do
        local t = date("!*t", (first + offsetDays) * 86400)
        local monthOffset = (t.year * 12 + t.month) - (shownYear * 12 + shownMonth)
        local ok, count = pcall(C_Calendar.GetNumDayEvents, monthOffset, t.day)
        seen = seen + ((ok and Plain(count)) or 0)
        for index = 1, (ok and Plain(count)) or 0 do
            local event = C_Calendar.GetDayEvent(monthOffset, t.day, index)
            if event then
                local title, start = Plain(event.title), event.startTime
                local hour, minute = Plain(start and start.hour), Plain(start and start.minute)
                if not (title and hour and minute) then return end
                if isGuildRaid(Plain(event.calendarType), Plain(event.eventType), Plain(event.sequenceType)) then
                    local at = T.BoardPane.WallTime(t.year, t.month, t.day, hour * 60 + minute)
                    if at + Raids.KEEP_AFTER >= now then
                        local creator = Plain(event.invitedBy)
                        if creator == "" then creator = nil end
                        local key = raidKey(Plain(event.eventID), creator, title, t.year, t.month, t.day)
                        local editable = C_Calendar.ContextMenuEventCanEdit
                            and select(2, pcall(C_Calendar.ContextMenuEventCanEdit, monthOffset, t.day, index))
                        local instanceName, difficultyName, instanceMap, difficultyId = listedInstance(event)
                        local kept = guild and guild.events[key]
                        if kept and kept.instanceName then
                            instanceName, difficultyName = kept.instanceName, kept.difficultyName
                            instanceMap, difficultyId = kept.instanceMap, kept.difficultyId
                        end
                        found[key] = { key = key, title = title, at = at, creator = creator,
                            year = t.year, month = t.month, day = t.day, modStatus = Plain(event.modStatus),
                            canEdit = Plain(editable) == true,
                            instanceName = instanceName, difficultyName = difficultyName,
                            instanceMap = instanceMap, difficultyId = difficultyId }
                    end
                end
            end
        end
    end

    if seen == 0 and next(found) == nil then return end
    raids = found
    if guild then guild.calendar = found end
    for key, raid in pairs(guild and found or {}) do
        if guild.events[key] then guild.events[key].at = raid.at end
    end
    changed()
    openNext()
end

local invites = {}
local queued, opened = {}, nil

local function calendarWindowShown()
    return CalendarFrame ~= nil and CalendarFrame:IsShown() == true
end

local function isOurs()
    local raid = opened and raids[opened.key]
    local info = raid and C_Calendar.GetEventInfo and C_Calendar.GetEventInfo()
    return info ~= nil and Plain(info.title) == raid.title
end

local function readInvites(key)
    if not (C_Calendar.GetNumInvites and C_Calendar.EventGetInvite) then return end
    local who = {}
    for i = 1, Plain(C_Calendar.GetNumInvites()) or 0 do
        local invite = C_Calendar.EventGetInvite(i)
        local name = invite and Plain(invite.name)
        if not name then return end
        local note = oneLine(Plain(invite.notes))
        who[T.Board.FullName(name)] = { classFile = Plain(invite.classFilename), status = Plain(invite.inviteStatus),
            modStatus = Plain(invite.modStatus), note = note ~= "" and note or nil }
    end
    local info = C_Calendar.GetEventInfo and C_Calendar.GetEventInfo()
    local description = info and oneLine(Plain(info.description))
    local instanceName, difficultyName, instanceMap, difficultyId
    if info then instanceName, difficultyName, instanceMap, difficultyId = openedInstance(info) end
    local guild, raid = store(), raids[key]
    if guild and raid and instanceName then
        local event = record(guild, key)
        event.at = event.at or raid.at
        event.instanceName, event.difficultyName = instanceName, difficultyName
        event.instanceMap, event.difficultyId = instanceMap, difficultyId
        raid.instanceName, raid.difficultyName = instanceName, difficultyName
        raid.instanceMap, raid.difficultyId = instanceMap, difficultyId
    end
    invites[key] = { readAt = GetServerTime(), who = who, note = description ~= "" and description or nil,
        instanceName = instanceName, difficultyName = difficultyName,
        instanceMap = instanceMap, difficultyId = difficultyId }
    changed()
end

local function closeOpened()
    if not opened then return end
    opened = nil
    if C_Calendar.CloseEvent then C_Calendar.CloseEvent() end
    openNext()
end

function openNext()
    if opened or #queued == 0 or calendarWindowShown() or not C_Calendar.OpenEvent then return end
    local item = table.remove(queued, 1)
    local raid = raids[item.key]
    local offset, day, index
    if raid then offset, day, index = locate(raid) end
    if not index then return openNext() end
    local this = { key = item.key, remove = item.remove }
    opened = this
    if not C_Calendar.OpenEvent(offset, day, index) then
        opened = nil
        return openNext()
    end
    C_Timer.After(INVITES_TIMEOUT, function()
        if opened == this then closeOpened() end
    end)
end

local function queueInvites(key, first, remove)
    local read = invites[key]
    if not (first or remove) and read and GetServerTime() - read.readAt < INVITES_FRESH then return end
    for i = #queued, 1, -1 do
        if queued[i].key == key then
            remove = remove or queued[i].remove
            table.remove(queued, i)
        end
    end
    local item = { key = key, remove = remove }
    if first or remove then table.insert(queued, 1, item) else queued[#queued + 1] = item end
end

function Raids:ReadInvites(key)
    queueInvites(key, true)
    openNext()
end

local function removeSignUp(key)
    local info = C_Calendar.GetEventInfo and C_Calendar.GetEventInfo()
    local canEdit = C_Calendar.EventCanEdit and Plain(C_Calendar.EventCanEdit())
    if not info or canEdit ~= false or not takesPart(Plain(info.inviteStatus)) or not C_Calendar.RemoveEvent then
        return
    end
    C_Calendar.RemoveEvent()
    C_Timer.After(READ_AFTER_ANSWER, function() Raids:ReadInvites(key) end)
end

local function onOpenEvent()
    if not opened then return end
    if not isOurs() then
        opened = nil
        return
    end
    if opened.remove then
        opened.remove = false
        removeSignUp(opened.key)
    end
    readInvites(opened.key)
end

local function onInviteList(_, complete)
    if not (opened and isOurs()) then return end
    readInvites(opened.key)
    if complete then closeOpened() end
end

function Raids:Load(withInvites)
    if C_Calendar and C_Calendar.OpenCalendar then C_Calendar.OpenCalendar() end
    self:Read()
    if not withInvites then return end
    local list = {}
    for _, raid in pairs(raids) do list[#list + 1] = raid end
    table.sort(list, function(a, b) return a.at < b.at end)
    for _, raid in ipairs(list) do queueInvites(raid.key, false) end
    openNext()
end

local function tellCalendar(key, signUp)
    local raid = raids[key]
    if not (raid and C_Calendar.ContextMenuSelectEvent) then return nil end
    local offset, day, index, event = locate(raid)
    if not index then return nil end
    if Plain(event.isLocked) then return "locked" end
    if signUp == takesPart(Plain(event.inviteStatus)) then return "done" end
    local signupType = Enum and Enum.CalendarInviteType and Enum.CalendarInviteType.Signup or 1
    local openToSignUps = Plain(event.inviteType) == signupType
    if not signUp and openToSignUps then
        if raid.canEdit or raid.modStatus == "CREATOR" or raid.modStatus == "MODERATOR" then return "manage" end
        queueInvites(key, true, true)
        openNext()
        return "done"
    end
    C_Calendar.ContextMenuSelectEvent(offset, day, index)
    if not signUp then
        C_Calendar.ContextMenuInviteDecline()
    elseif openToSignUps then
        C_Calendar.ContextMenuEventSignUp()
    else
        C_Calendar.ContextMenuInviteAvailable()
    end
    C_Timer.After(READ_AFTER_ANSWER, function() Raids:ReadInvites(key) end)
    return "done"
end

local function bump(entry)
    entry.ver = math.max(GetServerTime(), (entry.ver or 0) + 1)
end

function Raids:SignUp(key, role, note)
    local guild = store()
    local raid = raids[key]
    if not (guild and raid and ROLE_ORDER[role]) then return false end
    local event = record(guild, key)
    event.at = raid.at
    local player = me()
    local entry = event.signups[player]
    local kept = note ~= nil and Raids.CleanNote(note) or (entry and entry.note) or ""
    if not entry or entry.removed or entry.assigned then
        entry = { who = player, ver = entry and entry.ver,
            signedAt = entry and not entry.removed and entry.signedAt or GetServerTime() }
    end
    entry.role, entry.classFile, entry.itemLevel = role, Plain((UnitClassBase("player"))), ownItemLevel()
    entry.removed, entry.assigned = false, false
    entry.note = kept ~= "" and kept or nil
    bump(entry)
    event.signups[player] = entry
    sendEntry(key, entry)
    if tellCalendar(key, true) == "locked" then
        Addon:Print(T.L["%s is locked in the game's calendar: you are signed up in Raidex Keys only."]:format(raid.title))
    end
    changed()
    return true
end

function Raids:Withdraw(key)
    local guild = store()
    local event = guild and guild.events[key]
    local mine = event and event.signups[me()]
    if not mine or mine.removed then return false end
    mine.removed = true
    bump(mine)
    sendEntry(key, mine)
    if tellCalendar(key, false) == "manage" then
        Addon:Print(T.L["You can edit %s in the game's calendar: take your sign-up back there."]:format(raids[key].title))
    end
    changed()
    return true
end

function Raids:SetNote(key, note)
    local guild = store()
    local event = guild and guild.events[key]
    local mine = event and event.signups[me()]
    if not mine or mine.removed or mine.assigned then return false end
    note = Raids.CleanNote(note)
    if (mine.note or "") == note then return true end
    mine.note = note ~= "" and note or nil
    bump(mine)
    sendEntry(key, mine)
    changed()
    return true
end

function Raids:CanAssign(key)
    local raid = raids[key]
    if not raid then return false end
    return raid.modStatus == "CREATOR" or raid.modStatus == "MODERATOR" or raid.canEdit
        or (IsGuildLeader ~= nil and Plain(IsGuildLeader()) == true)
end

function Raids:SetRole(key, who, role)
    local guild = store()
    local raid = raids[key]
    if not (guild and raid and ROLE_ORDER[role] and who ~= me() and self:CanAssign(key)) then return false end
    local view, shown = self:Find(key), nil
    for _, s in ipairs(view and view.signups or {}) do
        if s.who == who then shown = s end
    end
    if not shown then return false end
    local event = record(guild, key)
    local entry = event.signups[who]
    if entry and not entry.removed and not entry.assigned then
        entry.role = role
        bump(entry)
        sendEntry(key, entry)
        changed()
        return true
    end
    event.at = raid.at
    entry = { who = who, role = role, classFile = shown.classFile, removed = false, assigned = true,
        signedAt = entry and entry.signedAt or GetServerTime(), ver = entry and entry.ver }
    bump(entry)
    event.signups[who] = entry
    sendEntry(key, entry)
    changed()
    return true
end

local PROGRESS_FRESH = 600
local PROGRESS_RETRY = 60
local PROGRESS_WAIT = 2
local PROGRESS_APART = 30
local JOURNAL_RETRY = 60

local journal
local journalAt
local raidBosses = {}
local bossesAt = {}
local progress = {}
local wanted = {}
local scanning, scannedAt

local function readJournal()
    local found = { byMap = {}, byName = {} }
    local theirs = EJ_GetCurrentTier()
    for tier = 1, Plain(EJ_GetNumTiers()) or 0 do
        EJ_SelectTier(tier)
        local i = 1
        while true do
            local instanceId, raidName, _, _, _, _, _, _, _, _, mapId = EJ_GetInstanceByIndex(i, true)
            instanceId, raidName, mapId = Plain(instanceId), Plain(raidName), Plain(mapId)
            if not instanceId then break end
            if mapId and not found.byMap[mapId] then found.byMap[mapId] = instanceId end
            if raidName and not found.byName[raidName] then found.byName[raidName] = instanceId end
            i = i + 1
        end
    end
    if theirs then EJ_SelectTier(theirs) end
    return found
end

local function lookup(name, mapId)
    if not journal then return nil end
    return mapId and journal.byMap[mapId] or name and journal.byName[name] or nil
end

local function journalInstance(name, mapId)
    local instanceId = lookup(name, mapId)
    if instanceId then return instanceId end
    if journalAt and GetTime() - journalAt < JOURNAL_RETRY then return nil end
    journalAt = GetTime()
    journal = readJournal()
    return lookup(name, mapId)
end

local function bossesOf(name, mapId)
    if not (EJ_GetNumTiers and EJ_SelectTier and EJ_GetCurrentTier and EJ_GetInstanceByIndex
        and EJ_SelectInstance and EJ_GetEncounterInfoByIndex) then
        return nil
    end
    local instanceId = journalInstance(name, mapId)
    if not instanceId then return nil end
    if raidBosses[instanceId] then return raidBosses[instanceId] end
    if bossesAt[instanceId] and GetTime() - bossesAt[instanceId] < JOURNAL_RETRY then return nil end
    bossesAt[instanceId] = GetTime()
    local theirs = EncounterJournal and EncounterJournal.instanceID
    EJ_SelectInstance(instanceId)
    local list, i = {}, 1
    while true do
        local boss, _, _, _, _, _, encounterId = EJ_GetEncounterInfoByIndex(i, instanceId)
        boss = Plain(boss)
        if not boss then break end
        list[#list + 1] = { name = boss, encounter = Plain(encounterId) }
        i = i + 1
    end
    if type(theirs) == "number" then EJ_SelectInstance(theirs) end
    if #list > 0 then raidBosses[instanceId] = list end
    return raidBosses[instanceId]
end

local function progressKey(raidName, difficultyName, mapId, difficultyId)
    local difficultyText = difficultyId and GetDifficultyInfo and Plain((GetDifficultyInfo(difficultyId)))
        or difficultyName
    if not ((raidName or mapId) and difficultyText) then return nil end
    return tostring(mapId or raidName) .. "|" .. tostring(difficultyId or difficultyText), difficultyText
end

local function stale(cacheKey)
    local held = progress[cacheKey]
    if not (held and held.at) then return true end
    local fresh = held.killed and held.killed > 0 and PROGRESS_FRESH or PROGRESS_RETRY
    return GetTime() - held.at >= fresh
end

local function criteriaOf(id)
    local list = {}
    for i = 1, GetAchievementNumCriteria and Plain(GetAchievementNumCriteria(id)) or 0 do
        local text, _, done, quantity, required, _, _, asset = GetAchievementCriteriaInfo(id, i)
        list[i] = { text = Plain(text), done = Plain(done), quantity = Plain(quantity),
            required = Plain(required), asset = Plain(asset) }
    end
    return list
end

function Raids.ReadProgress()
    scanning, scannedAt = nil, GetTime()
    if not (GetGuildCategoryList and GetCategoryNumAchievements and GetAchievementInfo) then return end

    local asked = {}
    for cacheKey, want in pairs(wanted) do
        if stale(cacheKey) then
            local bosses = bossesOf(want.raidName, want.mapId)
            if bosses and #bosses > 0 then
                local byEncounter, byName = {}, {}
                for i, boss in ipairs(bosses) do
                    if boss.encounter then byEncounter[boss.encounter] = i end
                    byName[boss.name] = i
                end
                asked[#asked + 1] = { cacheKey = cacheKey, difficultyText = want.difficultyText,
                    bosses = bosses, byEncounter = byEncounter, byName = byName, down = {} }
            end
        end
    end
    if #asked == 0 then return end

    for _, category in ipairs(GetGuildCategoryList() or {}) do
        for index = 1, Plain((GetCategoryNumAchievements(category, true))) or 0 do
            local id, name, _, completed, _, _, _, description = GetAchievementInfo(category, index)
            id, name, description = Plain(id), Plain(name) or "", Plain(description) or ""
            local title = name .. " " .. description
            local criteria
            for _, ask in ipairs(asked) do
                if id and title:find(ask.difficultyText, 1, true) then
                    criteria = criteria or criteriaOf(id)
                    for _, criterion in ipairs(criteria) do
                        local boss = criterion.asset and ask.byEncounter[criterion.asset]
                            or criterion.text and ask.byName[criterion.text]
                        if boss then
                            ask.spoken = true
                            if criterion.done or (criterion.quantity and criterion.required
                                and criterion.required > 0 and criterion.quantity >= criterion.required) then
                                ask.down[boss] = true
                            end
                        end
                    end
                    for i, boss in ipairs(ask.bosses) do
                        if name:find(boss.name, 1, true) then
                            ask.spoken = true
                            if Plain(completed) then ask.down[i] = true end
                        end
                    end
                end
            end
        end
    end

    local guild = store()
    for _, ask in ipairs(asked) do
        local held = { at = GetTime() }
        if ask.spoken then
            local killed = 0
            for _ in pairs(ask.down) do killed = killed + 1 end
            held.killed, held.bosses = killed, #ask.bosses
        end
        if guild then
            guild.progress = guild.progress or {}
            local kept = guild.progress[ask.cacheKey]
            if held.killed and (not kept or held.killed >= kept.killed) then
                guild.progress[ask.cacheKey] = { killed = held.killed, bosses = held.bosses }
            elseif kept then
                held.killed, held.bosses = kept.killed, kept.bosses
            end
        end
        progress[ask.cacheKey] = held
    end
    if T.Window then T.Window:Refresh() end
end

function Raids.ScanProgress()
    if scanning then return end
    scanning = true
    local since = scannedAt and GetTime() - scannedAt or PROGRESS_APART
    C_Timer.After(math.max(PROGRESS_WAIT, PROGRESS_APART - since), Raids.ReadProgress)
end

function Raids.Progress(raidName, difficultyName, mapId, difficultyId)
    local cacheKey, difficultyText = progressKey(raidName, difficultyName, mapId, difficultyId)
    if not cacheKey then return nil end
    wanted[cacheKey] = { raidName = raidName, mapId = mapId, difficultyText = difficultyText }
    local held = progress[cacheKey]
    if not held then
        local guild = store()
        local kept = guild and guild.progress and guild.progress[cacheKey]
        held = { killed = kept and kept.killed, bosses = kept and kept.bosses }
        progress[cacheKey] = held
    end
    if stale(cacheKey) then Raids.ScanProgress() end
    return held.killed, held.bosses
end

function Raids.ForgetProgress()
    progress = {}
end

function Raids.DebugCriteria(name, mapId, difficultyName, difficultyId)
    local bosses = bossesOf(name, mapId) or {}
    local difficultyText = difficultyId and GetDifficultyInfo and Plain((GetDifficultyInfo(difficultyId)))
        or difficultyName
    local byEncounter, byName, parts = {}, {}, {}
    for _, boss in ipairs(bosses) do
        if boss.encounter then byEncounter[boss.encounter] = true end
        byName[boss.name] = true
    end
    for _, category in ipairs(difficultyText and #bosses > 0 and GetGuildCategoryList() or {}) do
        for index = 1, Plain((GetCategoryNumAchievements(category, true))) or 0 do
            local id, title, _, _, _, _, _, description = GetAchievementInfo(category, index)
            id, title, description = Plain(id), Plain(title) or "", Plain(description) or ""
            if id and (title .. " " .. description):find(difficultyText, 1, true) then
                for criterion = 1, Plain(GetAchievementNumCriteria(id)) or 0 do
                    local text, kind, done, quantity, required, _, _, asset = GetAchievementCriteriaInfo(id, criterion)
                    if byEncounter[Plain(asset)] or byName[Plain(text)] then
                        parts[#parts + 1] = ("%s=%s/%s/%s/%s/%s"):format(tostring(Plain(text)), tostring(Plain(kind)),
                            tostring(Plain(done)), tostring(Plain(quantity)), tostring(Plain(required)), tostring(Plain(asset)))
                    end
                end
            end
        end
    end
    return #parts > 0 and table.concat(parts, "; ") or "no criterion of its bosses"
end

function Raids.DebugBosses(name, mapId)
    local bosses = bossesOf(name, mapId) or {}
    local ids = {}
    for _, boss in ipairs(bosses) do ids[#ids + 1] = tostring(boss.encounter) end
    return ("journal %s, %d bosses (%s)"):format(tostring(lookup(name, mapId)), #bosses, table.concat(ids, " "))
end

function Raids.Build(key, raid, signups, player, calendar)
    local list, counts, mine, done = {}, { T = 0, H = 0, D = 0 }, nil, {}
    local inGame = calendar and calendar.who or {}
    local function add(row)
        list[#list + 1] = row
        if row.role then counts[row.role] = counts[row.role] + 1 end
        if row.who == player then row.own, mine = true, row end
        done[row.who] = true
    end
    for who, s in pairs(signups or {}) do
        local seen = inGame[who]
        if s.removed then
            if not s.assigned and calendar and (s.ver or 0) >= calendar.readAt then done[who] = true end
        elseif not s.assigned or not calendar or (seen and takesPart(seen.status)) then
            add({ who = who, name = T.Exchange.ShortName(who), role = ROLE_ORDER[s.role] and s.role or nil,
                classFile = s.classFile or (seen and seen.classFile), itemLevel = s.itemLevel,
                signedAt = s.signedAt, assigned = s.assigned, note = s.note or (seen and seen.note) })
        end
    end
    for who, seen in pairs(inGame) do
        if not done[who] and takesPart(seen.status) then
            add({ who = who, name = T.Exchange.ShortName(who), classFile = seen.classFile, calendarOnly = true,
                note = seen.note })
        end
    end
    local creator = raid.creator and T.Board.FullName(raid.creator)
    for _, row in ipairs(list) do
        local seen = inGame[row.who]
        row.creator = (seen ~= nil and seen.modStatus == "CREATOR") or row.who == creator
    end
    table.sort(list, function(a, b)
        local left, right = ROLE_ORDER[a.role] or 4, ROLE_ORDER[b.role] or 4
        if left ~= right then return left < right end
        if (a.signedAt or 0) ~= (b.signedAt or 0) then return (a.signedAt or 0) < (b.signedAt or 0) end
        return a.who < b.who
    end)
    local opened = calendar and calendar.instanceName and calendar
    local instanceName = opened and opened.instanceName or raid.instanceName
    local difficultyName = opened and opened.difficultyName or raid.difficultyName
    local instanceMap = opened and opened.instanceMap or raid.instanceMap
    local difficultyId = opened and opened.difficultyId or raid.difficultyId
    return { key = key, title = raid.title, at = raid.at, creator = raid.creator,
        instanceName = instanceName, difficultyName = difficultyName,
        instanceMap = instanceMap, difficultyId = difficultyId,
        instance = instanceText(instanceName, difficultyName), note = calendar and calendar.note,
        signups = list, counts = counts, mine = mine }
end

function Raids:View()
    wanted = {}
    local guild = store()
    if not guild then return nil end
    if next(raids) == nil and guild.calendar then raids = guild.calendar end
    local now, player, list = GetServerTime(), me(), {}
    for key, raid in pairs(raids) do
        if raid.at + Raids.KEEP_AFTER >= now then
            local event = guild.events[key]
            local built = Raids.Build(key, raid, event and event.signups, player, invites[key])
            built.killed, built.bosses = Raids.Progress(built.instanceName, built.difficultyName,
                built.instanceMap, built.difficultyId)
            list[#list + 1] = built
        end
    end
    table.sort(list, function(a, b)
        if a.at ~= b.at then return a.at < b.at end
        return a.key < b.key
    end)
    return list
end

function Raids:Find(key)
    for _, raid in ipairs(self:View() or {}) do
        if raid.key == key then return raid end
    end
    return nil
end

function Raids:DebugInfo()
    local count, first = 0, nil
    for key in pairs(raids) do
        count = count + 1
        if not first or key < first then first = key end
    end
    return count, first
end

function Raids:Start()
    purge()
    C_ChatInfo.RegisterAddonMessagePrefix(PREFIX)
    Addon:On("CHAT_MSG_ADDON", onMessage)
    for _, event in ipairs({ "CALENDAR_UPDATE_EVENT_LIST", "CALENDAR_UPDATE_GUILD_EVENTS",
        "CALENDAR_NEW_EVENT", "CALENDAR_UPDATE_EVENT" }) do
        Addon:On(event, function()
            Addon:Debounce("raids-read", 1, function() Raids:Read() end)
        end)
    end
    Addon:On("CALENDAR_OPEN_EVENT", onOpenEvent)
    for _, event in ipairs({ "RECEIVED_ACHIEVEMENT_LIST", "CRITERIA_UPDATE", "ACHIEVEMENT_EARNED" }) do
        Addon:On(event, Raids.ForgetProgress)
    end
    Addon:On("CALENDAR_UPDATE_INVITE_LIST", onInviteList)
    C_Timer.After(LOAD_AFTER_LOGIN, function() Raids:Load() end)
    C_Timer.After(DIGEST_AFTER_LOGIN, function() Raids:SendDigest() end)
    C_Timer.NewTicker(DIGEST_EVERY, function() Raids:SendDigest() end)
end
