local _, T = ...
local L, Plain = T.L, T.Plain

local Inbox = {}
T.Inbox = Inbox

local KEEP_DONE = 30 * 86400

local asked = false

local function answers() return T.DB:Data().inbox end

local function requests()
    local inbox = RaidexKeysInbox
    return type(inbox) == "table" and type(inbox.requests) == "table" and inbox.requests or {}
end

local function done(id, state)
    answers()[id] = { state = state, at = GetServerTime() }
    requests()[id] = nil
end

local function purge()
    local now = GetServerTime()
    for id, answer in pairs(answers()) do
        if type(answer) ~= "table" or now - (answer.at or 0) > KEEP_DONE then answers()[id] = nil end
    end
end

function Inbox:Mine()
    local guid, list = Plain(UnitGUID("player")), {}
    for id, request in pairs(requests()) do
        if type(request) == "table" and request.guid == guid and not answers()[id] then
            request.id = id
            list[#list + 1] = request
        end
    end
    table.sort(list, function(a, b) return (a.at or 0) < (b.at or 0) end)
    return list
end

local ROLE = { T = "Tank", H = "Healer", D = "Damage" }

local function line(request, raid)
    if request.action == "withdraw" then
        return L["%s: withdraw"]:format(raid.title)
    end
    return L["%s: sign up as %s"]:format(raid.title, L[ROLE[request.role] or "Damage"])
end

function Inbox:Apply(list)
    for _, request in ipairs(list) do
        local ok
        if request.action == "withdraw" then
            ok = T.Raids:Withdraw(request.key)
        else
            ok = T.Raids:SignUp(request.key, request.role, request.note)
        end
        done(request.id, ok and "applied" or "failed")
    end
end

function Inbox:Dismiss(list)
    for _, request in ipairs(list) do done(request.id, "dismissed") end
end

function Inbox:Check()
    if asked then return end
    local view = T.Raids:View()
    if not view or #view == 0 then return end
    local list, lines = {}, {}
    for _, request in ipairs(self:Mine()) do
        local raid = T.Raids:Find(request.key)
        if raid then
            list[#list + 1] = request
            lines[#lines + 1] = line(request, raid)
        else
            done(request.id, "gone")
        end
    end
    if #list == 0 then return end
    asked = true
    local text = L["Raidex Keys on your computer asks:"] .. "\n\n" .. table.concat(lines, "\n")
    if not (StaticPopupDialogs and StaticPopup_Show) then return end
    StaticPopupDialogs.RAIDEXKEYS_INBOX = StaticPopupDialogs.RAIDEXKEYS_INBOX or {
        text = "%s", button1 = L["Apply"], button2 = L["Discard"],
        OnAccept = function(_, data) Inbox:Apply(data) end,
        OnCancel = function(_, data, reason)
            if reason == "clicked" then Inbox:Dismiss(data) end
        end,
        timeout = 0, whileDead = true, hideOnEscape = false, preferredIndex = 3,
    }
    StaticPopup_Show("RAIDEXKEYS_INBOX", text, nil, list)
end

function Inbox:Start()
    local db = T.DB:Data()
    db.inbox = type(db.inbox) == "table" and db.inbox or {}
    purge()
end
