local _, T = ...
local L, Plain = T.L, T.Plain

local MinimapButton = {}
T.MinimapButton = MinimapButton

local ICON = "Interface\\AddOns\\RaidexKeys\\Media\\Minimap"
local DEFAULT_ANGLE = 225
local GLOW = T.Themes.Colors.glow
local TIMER = T.Themes.Colors.gold
local MUTED = T.Themes.Colors.muted
local WHITE = T.Themes.Colors.text

local button

local function keyText(mapId, level)
    if not mapId then return L["No keystone"] end
    local name = C_ChallengeMode.GetMapUIInfo(mapId)
    return ("%s +%d"):format(name or ("#" .. mapId), level)
end

local function row(tooltip, label, value)
    tooltip:AddDoubleLine(label, value, MUTED[1], MUTED[2], MUTED[3], WHITE[1], WHITE[2], WHITE[3])
end

function MinimapButton.FillTooltip(tooltip, sample)
    tooltip:AddLine(L["Raidex Keys"], GLOW[1], GLOW[2], GLOW[3])
    if sample then
        row(tooltip, L["Your key"], ("%s +%d"):format(sample.dungeon or "?", sample.level))
        row(tooltip, L["Group"], L["%d / %d keys known"]:format(sample.known, sample.size))
        row(tooltip, L["Guild"], L["%d keys this week"]:format(sample.guildKeys))
    else
        row(tooltip, L["Your key"], keyText(T.Snapshot:OwnKey()))

        local known, size = T.Exchange:PartyKnown()
        row(tooltip, L["Group"], known and L["%d / %d keys known"]:format(known, size) or L["not in a group"])

        local guildKeys = T.Exchange:GuildKeysThisWeek()
        row(tooltip, L["Guild"], not IsInGuild() and L["not in a guild"]
            or guildKeys == 1 and L["1 key this week"]
            or L["%d keys this week"]:format(guildKeys))
    end

    tooltip:AddLine(" ")
    tooltip:AddLine(L["Left-click: open window"], TIMER[1], TIMER[2], TIMER[3])
    tooltip:AddLine(L["Shift-click: open options"], TIMER[1], TIMER[2], TIMER[3])
    tooltip:AddLine(L["Ctrl-click: show or hide the timer"], TIMER[1], TIMER[2], TIMER[3])

    if not sample then T.Exchange:RefreshParty() end
end

function MinimapButton.KeystoneLink()
    for bag = 0, NUM_BAG_SLOTS or 4 do
        for slot = 1, C_Container.GetContainerNumSlots(bag) do
            local itemId = C_Container.GetContainerItemID(bag, slot)
            if itemId and C_Item.IsItemKeystoneByID(itemId) then
                return Plain(C_Container.GetContainerItemLink(bag, slot))
            end
        end
    end
end

function MinimapButton.LinkInChat(link)
    if ChatFrameUtil.InsertLink(link) then return end
    ChatFrameUtil.OpenChat(link)
end

function MinimapButton.OnClick(_, mouseButton)
    if mouseButton == "RightButton" then
        local link = MinimapButton.KeystoneLink()
        if link then MinimapButton.LinkInChat(link) else T.Addon:Print(L["No keystone in your bags."]) end
    elseif T.Addon:ShiftDown() then
        T.OpenOptions()
    elseif T.Addon:ControlDown() then
        T.Timer:SetShown(not T.Timer.Wanted(), true)
    else
        T.Window:Toggle()
    end
end

local function place(angle)
    local rad = math.rad(angle)
    local rx = Minimap:GetWidth() / 2 + 10
    local ry = Minimap:GetHeight() / 2 + 10
    button:ClearAllPoints()
    button:SetPoint("CENTER", Minimap, "CENTER", math.cos(rad) * rx, math.sin(rad) * ry)
end

local function followCursor()
    local mx, my = Minimap:GetCenter()
    local cx, cy = GetCursorPosition()
    local scale = Minimap:GetEffectiveScale()
    local angle = math.deg(math.atan2(cy / scale - my, cx / scale - mx))
    T.DB:Settings().minimap.angle = angle
    place(angle)
end

local function create()
    button = CreateFrame("Button", "RaidexKeysMinimapButton", Minimap)
    button:SetSize(31, 31)
    button:SetFrameStrata("MEDIUM")
    button:SetFrameLevel(8)
    button:RegisterForClicks("LeftButtonUp", "RightButtonUp")
    button:RegisterForDrag("LeftButton")
    button:SetHighlightTexture(136477)

    local background = button:CreateTexture(nil, "BACKGROUND")
    background:SetSize(24, 24)
    background:SetTexture(136467)
    background:SetPoint("CENTER", 0, 1)

    local icon = button:CreateTexture(nil, "ARTWORK")
    icon:SetSize(18, 18)
    icon:SetTexture(ICON, "CLAMP", "CLAMP", "TRILINEAR")
    icon:SetPoint("CENTER", 0, 1)
    button.icon = icon

    local border = button:CreateTexture(nil, "OVERLAY")
    border:SetSize(50, 50)
    border:SetTexture(136430)
    border:SetPoint("TOPLEFT")

    button:SetScript("OnClick", MinimapButton.OnClick)
    button:SetScript("OnEnter", function(self)
        GameTooltip:SetOwner(self, "ANCHOR_LEFT")
        MinimapButton.FillTooltip(GameTooltip)
        GameTooltip:Show()
    end)
    button:SetScript("OnLeave", function() GameTooltip:Hide() end)
    button:SetScript("OnDragStart", function(self)
        GameTooltip:Hide()
        self:SetScript("OnUpdate", followCursor)
    end)
    button:SetScript("OnDragStop", function(self) self:SetScript("OnUpdate", nil) end)

    place(T.DB:Settings().minimap.angle)
end

local function registerBroker()
    local ldb = LibStub and LibStub("LibDataBroker-1.1", true)
    if not ldb then return end
    ldb:NewDataObject("Raidex Keys", {
        type = "launcher",
        icon = ICON,
        label = "Raidex Keys",
        OnClick = MinimapButton.OnClick,
        OnTooltipShow = MinimapButton.FillTooltip,
    })
end

function MinimapButton:Start()
    create()
    self:SetShown(not T.DB:Settings().minimap.hide)
    registerBroker()
end

function MinimapButton:ResetPosition()
    T.DB:Settings().minimap.angle = DEFAULT_ANGLE
    if button then place(DEFAULT_ANGLE) end
end

function MinimapButton:SetShown(shown)
    T.DB:Settings().minimap.hide = not shown
    if button then button:SetShown(shown) end
end
