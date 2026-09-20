local _, T = ...
local Addon, Plain = T.Addon, T.Plain

local Board = {}
T.Board = Board

local PREFIX = "RaidexKB"
Board.MAX_APPLICANTS = 10
Board.GROUP_SIZE = 5

local COUNT_YELLOW, COUNT_GREEN, COUNT_RED = "|cffffd100", "|cff1eff00", "|cffff4d4d"

function Board.CountText(signups)
    local count = signups + 1
    local color = count < Board.GROUP_SIZE and COUNT_YELLOW
        or count == Board.GROUP_SIZE and COUNT_GREEN or COUNT_RED
    return ("%s%d|r/%d"):format(color, count, Board.GROUP_SIZE)
end
Board.NOTE_MAX = 60
local KEEP_AFTER = 3 * 3600

local SEND_EVERY = 1.2
local DIGEST_AFTER_LOGIN = 20
local DIGEST_EVERY = 900
local REPLY_MIN, REPLY_SPREAD = 1, 4
local BUNDLE_TIMEOUT = 8
local PER_MESSAGE = 3
local ROLES = { T = "TANK", H = "HEALER", D = "DAMAGER" }

local function fullName(name)
    if not name then return nil end
    if name:find("-", 1, true) then return name end
    return name .. "-" .. (GetNormalizedRealmName() or "")
end

local function me()
    return fullName(Plain(UnitName("player")))
end
Board.FullName, Board.Me = fullName, me

local function store()
    if not IsInGuild() then return nil end
    local name, _, _, realm = GetGuildInfo("player")
    name = Plain(name)
    if not name then return nil end
    local key = name .. "-" .. (realm or GetRealmName())
    local boards = T.DB:Data().board
    boards[key] = boards[key] or { posts = {} }
    return boards[key]
end

local DIGITS = "0123456789abcdefghijklmnopqrstuvwxyz"

local function b36(n)
    n = math.floor(n or 0)
    if n <= 0 then return "0" end
    local out = ""
    while n > 0 do
        local d = n % 36
        out = DIGITS:sub(d + 1, d + 1) .. out
        n = math.floor(n / 36)
    end
    return out
end

local function num36(s) return tonumber(s or "", 36) end
Board.B36, Board.Num36 = b36, num36

local function hash(text)
    local h = 5381
    for i = 1, #text do h = (h * 33 + text:byte(i)) % 4294967296 end
    return ("%08x"):format(h)
end
Board.Hash = hash

function Board.CleanNote(note)
    note = (note or ""):gsub("[~|\r\n]", " "):gsub("^%s+", ""):gsub("%s+$", "")
    if #note <= Board.NOTE_MAX then return note end
    local cut = Board.NOTE_MAX
    while cut > 0 and note:byte(cut + 1) and note:byte(cut + 1) >= 128 and note:byte(cut + 1) < 192 do
        cut = cut - 1
    end
    return note:sub(1, cut)
end

local function entryText(s)
    return table.concat({ s.who, s.role or "D", s.classFile or "", b36(s.rating), b36(s.signedAt),
        b36(s.ver), s.removed and "1" or "0" }, ",")
end

local function parseEntry(text)
    local who, role, classFile, rating, signedAt, ver, removed =
        text:match("^([^,]+),([THD]),([^,]*),(%w+),(%w+),(%w+),([01])$")
    if not who then return nil end
    return { who = who, role = role, classFile = classFile ~= "" and classFile or nil,
        rating = num36(rating), signedAt = num36(signedAt), ver = num36(ver), removed = removed == "1" }
end

local function canonical(post)
    local parts = { post.id, post.holder, post.mapId, post.level, post.at, post.ver,
        post.removed and 1 or 0, post.note or "" }
    local whos = {}
    for who in pairs(post.signups) do whos[#whos + 1] = who end
    table.sort(whos)
    for _, who in ipairs(whos) do parts[#parts + 1] = entryText(post.signups[who]) end
    return table.concat(parts, "~")
end

local function postHead(post)
    return table.concat({ post.id, post.holder, post.mapId, post.level, post.at, post.ver,
        post.removed and 1 or 0, post.note or "" }, "~")
end

local function postHash(post) return hash(canonical(post)) end

local function newer(a, textA, b, textB)
    if (a.ver or 0) ~= (b.ver or 0) then return (a.ver or 0) > (b.ver or 0) end
    return textA > textB
end

local queue = {}
local sender

local function canSend()
    return IsInGuild() and not (IsInInstance and IsInInstance())
end

local function pump()
    if #queue == 0 then
        if sender then sender:Cancel() sender = nil end
        return
    end
    if not canSend() then return end
    local item = queue[1]
    local result = C_ChatInfo.SendAddonMessage(item.prefix, item.message, "GUILD")
    local results = Enum and Enum.SendAddonMessageResult
    if results and (result == results.AddonMessageThrottle or result == results.ChannelThrottle
        or result == results.AddOnMessageLockdown) then
        return
    end
    table.remove(queue, 1)
end

local function send(prefix, message)
    queue[#queue + 1] = { prefix = prefix, message = message }
    if not sender then
        pump()
        if #queue > 0 then sender = C_Timer.NewTicker(SEND_EVERY, pump) end
    end
end
Board.Send = send

local function enqueue(message)
    send(PREFIX, message)
end

local function sendBundle(post)
    local whos = {}
    for who in pairs(post.signups) do whos[#whos + 1] = who end
    table.sort(whos)
    enqueue(table.concat({ "P", post.id, postHash(post), post.holder, post.mapId, post.level,
        b36(post.at), b36(post.ver), post.removed and "1" or "0", #whos, post.note or "" }, "~"))
    for i = 1, #whos, PER_MESSAGE do
        local entries = {}
        for j = i, math.min(i + PER_MESSAGE - 1, #whos) do
            entries[#entries + 1] = entryText(post.signups[whos[j]])
        end
        enqueue("S~" .. post.id .. "~" .. table.concat(entries, ";"))
    end
end

local function expired(post, now)
    return (post.at or 0) + KEEP_AFTER <(now or GetServerTime())
end

local function purge()
    for _, guild in pairs(T.DB:Data().board) do
        for id, post in pairs(guild.posts) do
            if expired(post) then guild.posts[id] = nil end
        end
    end
end

local replies, heard = {}, {}

local function changed()
    Addon:Debounce("board", 0.5, function()
        Board:CheckOverflow()
        if T.Window then T.Window:Refresh() end
        if T.BoardPane then T.BoardPane:Refresh() end
    end)
end

local function scheduleReply(id)
    if replies[id] then return end
    local since = GetTime()
    replies[id] = since
    C_Timer.After(REPLY_MIN + math.random() * REPLY_SPREAD, function()
        replies[id] = nil
        local guild = store()
        local post = guild and guild.posts[id]
        if not post or expired(post) then return end
        local said = heard[id]
        if said and said.at >= since and said.hash == postHash(post) then return end
        sendBundle(post)
    end)
end

local bundles = {}

local function mergePost(guild, incoming)
    local post = guild.posts[incoming.id]
    if not post then
        incoming.signups = {}
        guild.posts[incoming.id] = incoming
        return true
    end
    if newer(incoming, postHead(incoming), post, postHead(post)) then
        post.holder, post.mapId, post.level, post.at = incoming.holder, incoming.mapId, incoming.level, incoming.at
        post.ver, post.removed, post.note = incoming.ver, incoming.removed, incoming.note
        return true
    end
    return false
end

local function mergeSignup(post, incoming)
    local own = post.signups[incoming.who]
    if own and not newer(incoming, entryText(incoming), own, entryText(own)) then return false end
    post.signups[incoming.who] = incoming
    return true
end

local function finishBundle(key)
    local bundle = bundles[key]
    if not bundle then return end
    bundles[key] = nil
    local guild = store()
    local post = guild and guild.posts[bundle.id]
    if not post then return end
    if postHash(post) ~= bundle.hash then scheduleReply(bundle.id) end
end

local digests = {}

local function onDigest(from, last, list)
    local seen = digests[from] or {}
    digests[from] = seen
    for id, h in (list or ""):gmatch("(%x+):(%x+)") do seen[id] = h end
    if last ~= "1" then return end
    digests[from] = nil
    local guild = store()
    if not guild then return end
    for id, post in pairs(guild.posts) do
        if not expired(post) and seen[id] ~= postHash(post) then scheduleReply(id) end
    end
end

local function onMessage(_, prefix, message, channel, from)
    if prefix ~= PREFIX or channel ~= "GUILD" then return end
    message, from = Plain(message), fullName(Plain(from))
    if not (message and from) or from == me() then return end
    T.Exchange:HeardFrom(from)
    local guild = store()
    if not guild then return end

    local kind = message:sub(1, 1)
    if kind == "P" then
        local id, h, holder, mapId, level, at, ver, removed, count, note =
            message:match("^P~(%x+)~(%x+)~([^~]+)~(%d+)~(%d+)~(%w+)~(%w+)~([01])~(%d+)~(.*)$")
        if not id then return end
        local post = { id = id, holder = holder, mapId = tonumber(mapId), level = tonumber(level),
            at = num36(at), ver = num36(ver), removed = removed == "1", note = Board.CleanNote(note) }
        if expired(post) then return end
        heard[id] = { hash = h, at = GetTime() }
        if mergePost(guild, post) then changed() end
        local key = from .. id
        bundles[key] = { id = id, hash = h, expected = tonumber(count), got = 0 }
        if bundles[key].expected == 0 then
            finishBundle(key)
        else
            C_Timer.After(BUNDLE_TIMEOUT, function() finishBundle(key) end)
        end
    elseif kind == "S" or kind == "U" then
        local id, list = message:match("^[SU]~(%x+)~(.+)$")
        local post = id and guild.posts[id]
        if not post then return end
        local key = from .. id
        for text in list:gmatch("[^;]+") do
            local entry = parseEntry(text)
            if entry then
                if mergeSignup(post, entry) then changed() end
                if kind == "S" and bundles[key] then bundles[key].got = bundles[key].got + 1 end
            end
        end
        local bundle = kind == "S" and bundles[key]
        if bundle and bundle.got >= bundle.expected then finishBundle(key) end
    elseif kind == "D" then
        local last, list = message:match("^D~([01])~(.*)$")
        if last then onDigest(from, last, list) end
    end
end

function Board:SendDigest()
    local guild = store()
    if not guild then return end
    purge()
    local items = {}
    for id, post in pairs(guild.posts) do items[#items + 1] = id .. ":" .. postHash(post) end
    table.sort(items)
    local part = {}
    local function flush(last)
        enqueue(("D~%s~%s"):format(last and "1" or "0", table.concat(part, ",")))
        part = {}
    end
    for _, item in ipairs(items) do
        part[#part + 1] = item
        if #part >= 12 then flush(false) end
    end
    flush(true)
end

local function bump(record)
    record.ver = math.max(GetServerTime(), (record.ver or 0) + 1)
end

local function applicantsOf(post)
    local list = {}
    for _, s in pairs(post.signups or {}) do
        if not s.removed and s.who ~= post.holder then list[#list + 1] = s end
    end
    table.sort(list, function(a, b)
        if (a.signedAt or 0) ~= (b.signedAt or 0) then return (a.signedAt or 0) < (b.signedAt or 0) end
        return a.who < b.who
    end)
    for i = #list, Board.MAX_APPLICANTS + 1, -1 do list[i] = nil end
    return list
end

function Board.HolderEntry(post)
    local entry = post and post.signups and post.holder and post.signups[post.holder]
    if not entry or entry.removed then return nil end
    return entry
end

function Board:Post(at, note)
    local guild = store()
    local mapId, level = T.Snapshot:OwnKey()
    if not (guild and mapId) then return nil end
    local holder = me()
    local now = GetServerTime()
    local post = {
        id = hash(("%s:%d:%d"):format(holder, now, math.random(1, 1000000))),
        holder = holder, mapId = mapId, level = level, at = at,
        note = Board.CleanNote(note), ver = now, removed = false, signups = {},
    }
    guild.posts[post.id] = post
    sendBundle(post)
    changed()
    return post
end

function Board:Remove(id)
    local guild = store()
    local post = guild and guild.posts[id]
    if not post or post.holder ~= me() then return false end
    post.removed = true
    bump(post)
    sendBundle(post)
    changed()
    return true
end

local function playerRole()
    local spec = GetSpecialization and GetSpecialization()
    local role = spec and GetSpecializationRole and GetSpecializationRole(spec)
    for letter, name in pairs(ROLES) do
        if name == role then return letter end
    end
    return "D"
end

function Board:SignUp(id, role)
    local guild = store()
    local post = guild and guild.posts[id]
    local player = me()
    if not post or post.removed then return false end
    local mine = post.signups[player]
    if post.holder ~= player and not (mine and not mine.removed)
        and #applicantsOf(post) >= Board.MAX_APPLICANTS then
        return false
    end
    local classFile = UnitClassBase("player")
    local entry = mine or { who = player }
    entry.role = ROLES[role] and role or playerRole()
    entry.classFile = Plain(classFile)
    entry.rating = math.floor(T.Snapshot:Rating() + 0.5)
    if not mine or mine.removed then entry.signedAt = GetServerTime() end
    entry.removed = false
    bump(entry)
    post.signups[player] = entry
    enqueue("U~" .. id .. "~" .. entryText(entry))
    changed()
    return true
end

function Board:Withdraw(id)
    local guild = store()
    local post = guild and guild.posts[id]
    local mine = post and post.signups[me()]
    if not mine or mine.removed then return false end
    mine.removed = true
    bump(mine)
    enqueue("U~" .. id .. "~" .. entryText(mine))
    changed()
    return true
end

local toldFull = {}

function Board:CheckOverflow()
    local guild = store()
    if not guild then return end
    local player = me()
    for id, post in pairs(guild.posts) do
        local mine = post.signups[player]
        if mine and not mine.removed and not post.removed and post.holder ~= player then
            local inside = false
            for _, s in ipairs(applicantsOf(post)) do
                if s.who == player then inside = true end
            end
            if not inside then
                self:Withdraw(id)
                if not toldFull[id] then
                    toldFull[id] = true
                    Addon:Print(T.L["The key of %s filled up just before you: your sign-up was taken back."]:format(
                        (post.holder:gsub("%-.*$", ""))))
                end
            end
        end
    end
end

function Board:View()
    local guild = store()
    if not guild then return nil end
    local player, now, list = me(), GetServerTime(), {}
    for id, post in pairs(guild.posts) do
        if not post.removed and not expired(post, now) then
            local applicants = applicantsOf(post)
            local holder = Board.HolderEntry(post)
            local signedUp = false
            for _, s in ipairs(applicants) do
                if s.who == player then signedUp = true end
            end
            list[#list + 1] = {
                id = id, holder = post.holder, who = post.holder, name = T.Exchange.ShortName(post.holder),
                role = holder and holder.role, rating = holder and holder.rating,
                classFile = T.Exchange:ClassOf(post.holder), mapId = post.mapId, level = post.level,
                at = post.at, note = post.note, applicants = applicants,
                own = post.holder == player, signedUp = signedUp,
            }
        end
    end
    table.sort(list, function(a, b)
        if a.at ~= b.at then return a.at < b.at end
        return a.id < b.id
    end)
    return list
end

function Board:Find(id)
    for _, post in ipairs(self:View() or {}) do
        if post.id == id then return post end
    end
    return nil
end

function Board:Start()
    purge()
    C_ChatInfo.RegisterAddonMessagePrefix(PREFIX)
    Addon:On("CHAT_MSG_ADDON", onMessage)
    Addon:On("PLAYER_ENTERING_WORLD", function()
        if #queue > 0 and not sender then sender = C_Timer.NewTicker(SEND_EVERY, pump) end
    end)
    C_Timer.After(DIGEST_AFTER_LOGIN, function() Board:SendDigest() end)
    C_Timer.NewTicker(DIGEST_EVERY, function() Board:SendDigest() end)
end
