local ADDON, T = ...
local L = T.L

local category, layout
local root
local ours = {}
local timerToggle

function T.SetTimerEnabled(on)
    if timerToggle and timerToggle.SetValue then
        timerToggle:SetValue(on)
        return
    end
    T.DB:Settings().timer.enabled = on
    T.Timer:Update()
end

RaidexKeysAboutMixin = {}

local function pickColor(color, changed)
    local before = { color[1], color[2], color[3] }
    local function apply(r, g, b)
        color[1], color[2], color[3] = r, g, b
        T.Timer:Restyle()
        if changed then changed() end
    end
    local info = {
        r = color[1], g = color[2], b = color[3], hasOpacity = false,
        swatchFunc = function() apply(ColorPickerFrame:GetColorRGB()) end,
        cancelFunc = function() apply(before[1], before[2], before[3]) end,
    }
    if ColorPickerFrame.SetupColorPickerAndShow then
        ColorPickerFrame:SetupColorPickerAndShow(info)
        return
    end
    ColorPickerFrame.func, ColorPickerFrame.cancelFunc = info.swatchFunc, info.cancelFunc
    ColorPickerFrame.hasOpacity = false
    ColorPickerFrame:SetColorRGB(color[1], color[2], color[3])
    ColorPickerFrame:Show()
end

RaidexKeysColorMixin = {}

function RaidexKeysColorMixin:Init(initializer)
    local data = initializer:GetData()
    self.Label:SetText(data.label)
    self.Swatch.Edge:SetColorTexture(0, 0, 0, 1)
    self.Swatch:SetScript("OnClick", function()
        pickColor(data.color(), function() self:Repaint(data) end)
    end)
    self:Repaint(data)
end

function RaidexKeysColorMixin:Repaint(data)
    local color = data.color()
    self.Swatch.Fill:SetColorTexture(color[1], color[2], color[3], 1)
end

local CHIP_TEXT = "Made with <3 in Europe"
local CHIP_GAP = 6
local CHIP_HEIGHT = 18
local CHIP_PAD_X = 8

function RaidexKeysAboutMixin:Init(initializer)
    self.Text:SetText(initializer:GetData().text)
    local chip = self.Chip
    chip.Label:SetText(CHIP_TEXT)
    chip:ClearAllPoints()
    chip:SetPoint("TOPLEFT", self.Text, "BOTTOMLEFT", 0, -CHIP_GAP)
    chip:SetSize(math.ceil(chip.Label:GetStringWidth()) + 2 * CHIP_PAD_X, CHIP_HEIGHT)
end

local function metadata(field)
    local value = C_AddOns.GetAddOnMetadata(ADDON, field)
    if value == "" then return nil end
    return value
end

local GREY = "|cff808080"

local function aboutText()
    local lines = {}
    local title = metadata("Title")
    if title then lines[#lines + 1] = title end
    local version = metadata("Version")
    if version then
        lines[#lines + 1] = L["Version: %s (build %s)"]:format(version, metadata("X-Build") or "dev")
    end
    local copyright = metadata("X-Copyright")
    if copyright then lines[#lines + 1] = "\194\169 " .. copyright end
    local website = metadata("X-Website")
    if website then lines[#lines + 1] = L["More: %s"]:format((website:gsub("^%a+://", ""):gsub("/$", ""))) end
    lines[#lines + 1] = GREY .. L["%s, without any warranty"]:format(metadata("X-License") or "MIT") .. "|r"
    lines[#lines + 1] = GREY .. L["Window font: Marcellus by Astigmatic, SIL Open Font License"] .. "|r"
    lines[#lines + 1] = GREY .. L["PT Sans Narrow by ParaType, SIL Open Font License"] .. "|r"
    return table.concat(lines, "\n")
end

local ABOUT_BOTTOM_PAD = 8
local measure
local function textHeight(text)
    if not measure then
        measure = UIParent:CreateFontString(nil, "ARTWORK", "GameFontHighlightSmall")
        measure:SetJustifyH("LEFT")
        measure:Hide()
    end
    measure:SetText(text)
    return measure:GetStringHeight()
end

local function addAbout()
    if not Settings.CreateElementInitializer then return end
    local text = aboutText()
    local initializer = Settings.CreateElementInitializer("RaidexKeysAboutTemplate", { text = text })
    local extent = math.ceil(textHeight(text)) + CHIP_GAP + CHIP_HEIGHT + ABOUT_BOTTOM_PAD
    initializer.GetExtent = function() return extent end
    layout:AddInitializer(initializer)
end

local function page(name)
    if not Settings.RegisterVerticalLayoutSubcategory then return end
    category, layout = Settings.RegisterVerticalLayoutSubcategory(root, name)
    ours[category] = true
end

local function section(title)
    if CreateSettingsListSectionHeaderInitializer then
        layout:AddInitializer(CreateSettingsListSectionHeaderInitializer(title))
    end
end

local function element(template, data, height)
    if not Settings.CreateElementInitializer then return end
    local initializer = Settings.CreateElementInitializer(template, data)
    initializer.GetExtent = function() return height end
    layout:AddInitializer(initializer)
end

local function slider(key, name, tooltip, low, high, step, default, get, set, format, after)
    if not (Settings.CreateSlider and Settings.CreateSliderOptions) then return end
    local setting = Settings.RegisterProxySetting(category, ADDON .. "_" .. key,
        Settings.VarType.Number, name, default, get, function(value)
            set(value)
            if after then after() else T.Timer:Restyle() end
        end)
    local options = Settings.CreateSliderOptions(low, high, step)
    if options.SetLabelFormatter and MinimalSliderWithSteppersMixin then
        options:SetLabelFormatter(MinimalSliderWithSteppersMixin.Label.Right, format)
    end
    Settings.CreateSlider(category, setting, options, tooltip)
end

local function checkbox(key, variable, table, name, default, tooltip, changed)
    local setting = Settings.RegisterAddOnSetting(category, ADDON .. "_" .. key, variable,
        table, Settings.VarType.Boolean, name, default)
    if changed then setting:SetValueChangedCallback(changed) end
    Settings.CreateCheckbox(category, setting, tooltip)
    return setting
end

local function toggle(key, name, tooltip, default, get, set)
    if not (Settings.RegisterProxySetting and Settings.CreateCheckbox) then return end
    local setting = Settings.RegisterProxySetting(category, ADDON .. "_" .. key,
        Settings.VarType.Boolean, name, default, get, function(value)
            set(value)
            T.Timer:Restyle()
        end)
    Settings.CreateCheckbox(category, setting, tooltip)
end

local function addGeneral(settings)
    addAbout()

    if Settings.CreateDropdown then
        local setting = Settings.RegisterProxySetting(category, ADDON .. "_theme",
            Settings.VarType.String, L["Colour theme"], T.Themes.DEFAULT,
            T.Themes.Current, T.Themes.Select)
        Settings.CreateDropdown(category, setting, function()
            local container = Settings.CreateControlTextContainer()
            for _, theme in ipairs(T.Themes.List) do
                container:Add(theme.key, theme.name)
            end
            return container:GetData()
        end, L["Colours for the window, board, timer and tooltip. Choosing a theme resets the timer background and text colours; you can customise them afterwards."])
    end

    checkbox("shareWithGuild", "shareWithGuild", settings, L["Share key with guild"], true,
        L["Guild members see your keystone through the LibKeystone protocol, as with BigWigs."],
        function(_, value) T.Exchange:ShareChanged(value) end)

    do
        local setting = Settings.RegisterProxySetting(category, ADDON .. "_showMinimap",
            Settings.VarType.Boolean, L["Show minimap button"], true,
            function() return not settings.minimap.hide end,
            function(value) T.MinimapButton:SetShown(value) end)
        Settings.CreateCheckbox(category, setting)
    end

    slider("grid", L["Grid when moving"],
        L["The window and the timer move in steps of this many points while you drag them, so they line up. Off moves them freely."],
        0, T.GRID_MAX, 10, T.GRID_DEFAULT,
        function() return settings.grid or T.GRID_DEFAULT end,
        function(value) settings.grid = value end,
        function(value) return value == 0 and L["off"] or tostring(value) end,
        function() end)

    checkbox("gridResize", "gridResize", settings, L["Grid when resizing"], true,
        L["The window's size changes in the same steps while you drag its lower right corner."])
end

local function addWindow(settings)
    local window = settings.window
    if Settings.CreateDropdown then
        local setting = Settings.RegisterProxySetting(category, ADDON .. "_windowFont",
            Settings.VarType.String, L["Window font"], T.Window.DEFAULT_FONT,
            function() return window.font or T.Window.DEFAULT_FONT end,
            function(value)
                window.font = value
                T.Window:Restyle()
            end)
        Settings.CreateDropdown(category, setting, function()
            local container = Settings.CreateControlTextContainer()
            for _, font in ipairs(T.Fonts.Available(window.font)) do
                container:Add(font.key, font.name)
            end
            return container:GetData()
        end, L["Choose independently for this display. Additional fonts appear when an installed SharedMedia provider supplies them."])
    end

    slider("windowFontSize", L["Window font size"],
        L["The size of the window's text. Rows, columns and the window itself grow and shrink with it; its lower right corner drags it bigger or smaller."],
        T.Window.SIZE_MIN, T.Window.SIZE_MAX, 1, T.Window.DEFAULT_SIZE,
        function() return window.fontSize or T.Window.DEFAULT_SIZE end,
        function(value) window.fontSize = value end,
        function(value) return tostring(value) end,
        function() T.Window:Restyle() end)

    local function formatChoice(key, name, tooltip, choices)
        if not Settings.CreateDropdown then return end
        local setting = Settings.RegisterProxySetting(category, ADDON .. "_" .. key,
            Settings.VarType.String, name, "auto",
            function() return window[key] or "auto" end,
            function(value)
                window[key] = value
                T.Window:Refresh()
            end)
        Settings.CreateDropdown(category, setting, function()
            local container = Settings.CreateControlTextContainer()
            for _, choice in ipairs(choices) do container:Add(choice[1], choice[2]) end
            return container:GetData()
        end, tooltip)
    end
    formatChoice("timeFormat", L["Time format"],
        L["How the key board and the raid calendar write a time. \"As the game's clock\" follows the 24-hour setting of the game's own clock."], {
            { "auto", L["As the game's clock"] },
            { "24", L["24 hours (22:15)"] },
            { "12", L["12 hours (10:15 PM)"] },
        })
    formatChoice("dateFormat", L["Date format"],
        L["How the key board and the raid calendar write a day. \"As the game's language\" writes it the way the language of your game does."], {
            { "auto", L["As the game's language"] },
            { "dmy", L["Day.Month. (13.09.)"] },
            { "mdy", L["Month/Day (09/13)"] },
            { "iso", L["Year-Month-Day (2026-09-13)"] },
        })
end

local function addTimer(timer)
    section(L["During a key"])
    timerToggle = checkbox("timer", "enabled", timer, L["Show Mythic+ timer"], true,
        L["While a keystone runs: time left, the +2 and +3 limits, bosses, enemy forces and deaths. It stands there for as long as these options are open, to drag into place."],
        function()
            T.DB:Settings().timer.hidden = nil
            T.Timer:Update()
        end)
    checkbox("timerHideBlizzard", "hideBlizzard", timer, L["Hide the game's own timer"], true,
        L["Only while the Raidex Keys timer shows a run."],
        function() T.Timer:Update() end)
    checkbox("timerHideQuests", "hideQuests", timer, L["Hide the quest tracker"], false,
        L["While a keystone runs the tracked quests, campaigns and achievements go, and come back when the run ends. Untick it and the tracker stays where it was."],
        function() T.Timer:Update() end)

    section(L["In the chat"])
    checkbox("timerAnnounce", "announce", timer, L["The run's end in the group chat"], true,
        L["At the end the key's holder tells the group how it went, once the game lets addons speak again. Only while the Mythic+ timer is shown."])
    checkbox("timerBossTimes", "bossTimes", timer, L["Boss times in your chat"], true,
        L["After every boss, for you alone: when it fell, and where your M+ rating would land. Only while the Mythic+ timer is shown."])

    section(L["Preview"])
    if Settings.CreateDropdown then
        local setting = Settings.RegisterProxySetting(category, ADDON .. "_previewKey",
            Settings.VarType.String, L["Key in the preview"], "own",
            function() return T.Timer.PreviewChoice().map end,
            function(value) T.Timer:SetPreviewChoice(value, nil) end)
        Settings.CreateDropdown(category, setting, function()
            local container = Settings.CreateControlTextContainer()
            container:Add("own", L["Your key"])
            for _, map in ipairs(T.Timer.SeasonDungeons()) do
                container:Add(tostring(map.mapId), map.name)
            end
            container:Add("letters", L["Letter test"])
            return container:GetData()
        end, L["Which keystone the timer shows while these options are open: the one in your bags, any dungeon of the season, or a line of letters to see what the chosen font makes of umlauts."])
    end
    slider("previewLevel", L["Level in the preview"],
        L["The keystone level of a dungeon picked above. Your own key keeps its own level."],
        2, 20, 1, T.Timer.DEFAULT_PREVIEW_LEVEL,
        function() return T.Timer.PreviewChoice().level end,
        function(value) T.Timer:SetPreviewChoice(nil, value) end,
        function(value) return "+" .. value end)

    section(L["Look"])
    local Timer = T.Timer
    local function percent(value) return math.floor(value * 100 + 0.5) end

    toggle("timerAffixes", L["Show the affixes"],
        L["The week's affixes under the dungeon's name, running on across the card's width."],
        Timer.DEFAULT_AFFIXES,
        function() return timer.showAffixes ~= false end,
        function(value) timer.showAffixes = value end)

    toggle("timerScore", L["Show what the run is worth"],
        L["The line at the foot: the rating the run would give if it ended this moment, and what that would add."],
        Timer.DEFAULT_SCORE,
        function() return timer.showScore ~= false end,
        function(value) timer.showScore = value end)

    slider("timerBosses", L["Bosses in the list"],
        L["How many bosses the timer lists at once. The list follows the run: it begins at the first boss still standing, so what is next is always on it."],
        Timer.BOSSES_MIN, Timer.BOSSES_MAX, 1, Timer.DEFAULT_BOSSES,
        function() return timer.maxBosses or Timer.DEFAULT_BOSSES end,
        function(value) timer.maxBosses = value end,
        function(value) return value >= Timer.BOSSES_MAX and L["all"] or tostring(value) end)

    slider("timerAlpha", L["Timer opacity"],
        L["How much of the dungeon shows through the timer's background."],
        math.floor(Timer.ALPHA_MIN * 100), math.floor(Timer.ALPHA_MAX * 100), 5,
        percent(Timer.DEFAULT_ALPHA),
        function() return percent(timer.alpha or Timer.DEFAULT_ALPHA) end,
        function(value) timer.alpha = value / 100 end,
        function(value) return ("%d %%"):format(value) end)

    slider("timerFontSize", L["Timer font size"],
        L["The size of the body text. Head, clock and the small lines follow it."],
        Timer.SIZE_MIN, Timer.SIZE_MAX, 1, Timer.DEFAULT_SIZE,
        function() return timer.fontSize or Timer.DEFAULT_SIZE end,
        function(value) timer.fontSize = value end,
        function(value) return tostring(value) end)

    slider("timerScale", L["Timer size"],
        L["The whole card, larger or smaller. The font size sets the text inside it; this sets how big the card itself is drawn."],
        math.floor(Timer.SCALE_MIN * 100), math.floor(Timer.SCALE_MAX * 100), 5,
        percent(Timer.DEFAULT_SCALE),
        function() return percent(timer.scale or Timer.DEFAULT_SCALE) end,
        function(value) timer.scale = value / 100 end,
        function(value) return ("%d %%"):format(value) end)

    slider("timerSpacing", L["Timer line spacing"],
        L["How far the timer's lines stand apart. Below 100 % the card is tighter than the design draws it."],
        math.floor(Timer.SPACING_MIN * 100), math.floor(Timer.SPACING_MAX * 100), 5,
        percent(Timer.DEFAULT_SPACING),
        function() return percent(timer.spacing or Timer.DEFAULT_SPACING) end,
        function(value) timer.spacing = value / 100 end,
        function(value) return ("%d %%"):format(value) end)

    if Settings.CreateDropdown then
        local setting = Settings.RegisterProxySetting(category, ADDON .. "_timerFont",
            Settings.VarType.String, L["Timer font"], Timer.DEFAULT_FONT,
            function() return timer.font or Timer.DEFAULT_FONT end,
            function(value)
                timer.font = value
                T.Timer:Restyle()
            end)
        Settings.CreateDropdown(category, setting, function()
            local container = Settings.CreateControlTextContainer()
            for _, font in ipairs(T.Fonts.Available(timer.font)) do
                container:Add(font.key, font.name)
            end
            return container:GetData()
        end, L["Choose independently for this display. Additional fonts appear when an installed SharedMedia provider supplies them."])
    end

    element("RaidexKeysColorTemplate",
        { label = L["Timer background"], color = function() return timer.background end }, 26)
    element("RaidexKeysColorTemplate",
        { label = L["Timer text"], color = function() return timer.text end }, 26)
end

local DEFAULTS_METHODS = { "SetCurrentCategorySettingsToDefaults", "SetAllSettingsToDefaults" }
local defaultsHooked = false

function T.ResetToDefaults()
    T.Themes.Select(T.Themes.DEFAULT)
    T.Timer:ResetLook()
    T.Timer:ResetPosition()
    T.Window:ResetPosition()
    T.MinimapButton:ResetPosition()
    local window = T.DB:Settings().window
    window.font, window.fontSize = T.Window.DEFAULT_FONT, T.Window.DEFAULT_SIZE
    window.timeFormat, window.dateFormat = "auto", "auto"
    window.w, window.h = nil, nil
    T.DB:Settings().grid = T.GRID_DEFAULT
    T.DB:Settings().gridResize = true
    T.Window:Restyle()
end

function T.DefaultsHooked()
    return defaultsHooked
end

local function hookDefaults()
    local panel = SettingsPanel
    if defaultsHooked or not (panel and hooksecurefunc) then return end
    for _, method in ipairs(DEFAULTS_METHODS) do
        if type(panel[method]) == "function" then
            hooksecurefunc(panel, method, function(self)
                local current = self.GetCurrentCategory and self:GetCurrentCategory()
                if method == "SetAllSettingsToDefaults" or ours[current] then
                    T.ResetToDefaults()
                end
            end)
            defaultsHooked = true
        end
    end
end

function T.WatchOptionsPanel()
    hookDefaults()
    local panel = SettingsPanel
    if not (root and panel and panel.HookScript and panel.GetCurrentCategory) then return end
    local standing
    local function refresh()
        local open = panel:IsShown() and ours[panel:GetCurrentCategory()] == true
        if open == standing then return end
        standing = open
        T.Timer:SetOptionsPreview(open)
    end
    panel:HookScript("OnShow", refresh)
    panel:HookScript("OnHide", refresh)
    panel:HookScript("OnUpdate", refresh)
end

function T.SetupOptions()
    if root or not Settings or not Settings.RegisterVerticalLayoutCategory then return end
    local settings = T.DB:Settings()

    category, layout = Settings.RegisterVerticalLayoutCategory(L["Raidex Keys"])
    root = category
    ours[root] = true
    addGeneral(settings)

    page(L["Window"])
    addWindow(settings)

    page(L["Mythic+ timer"])
    addTimer(settings.timer)

    Settings.RegisterAddOnCategory(root)
end

function T.OpenOptions()
    if root then Settings.OpenToCategory(root:GetID()) end
end
