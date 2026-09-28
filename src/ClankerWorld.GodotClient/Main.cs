using ClankerWorld.GodotClient.ClientState;
using ClankerWorld.GodotClient.Pairing;
using ClankerWorld.GodotClient.UI;
using Godot;
using System.Globalization;

namespace ClankerWorld.GodotClient;

/// <summary>
/// The deliberately practical Phase 2 owner client. It renders only signed,
/// server-issued world projections; all control buttons submit a one-use
/// device-key proof to the server and never mutate a local simulation copy.
/// </summary>
public partial class Main : Control
{
    private const int DefaultTileSize = 96;
    private const int TileGap = 0;
    private const int RefreshSeconds = 1;

    private readonly System.Net.Http.HttpClient httpClient = new();
    private readonly OwnerWorldApi ownerApi;
    private readonly OwnerWorldObservationSession observationSession = new();
    private readonly OwnerDeviceRegistrationStore registrationStore = new();
    private readonly OwnerPendingSubmissionStore pendingSubmissionStore = new(
        ProjectSettings.GlobalizePath("user://owner-pending-submission.json"));
    private readonly GameDisplayPreferencesStore displayPreferencesStore = new(
        ProjectSettings.GlobalizePath("user://game-display-preferences.json"));
    private readonly Dictionary<long, OwnerWorldEvent> knownEvents = [];
    private readonly Dictionary<string, AgentMarker> inhabitantVisuals = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Label> mapObjectVisuals = new(StringComparer.Ordinal);
    private readonly Dictionary<string, float> inhabitantCanonicalXs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, float> mapObjectCanonicalXs = new(StringComparer.Ordinal);

    private readonly Label statusLabel = new();
    private readonly PanelContainer statusToast = new();
    private readonly PanelContainer connectionPanel = new();
    private readonly Button settingsButton = new();
    private readonly Button modLibraryButton = new();
    private readonly Button gameSettingsCategoryButton = new();
    private readonly Button worldSettingsCategoryButton = new();
    private readonly VBoxContainer gameSettingsContent = new();
    private readonly VBoxContainer worldSettingsContent = new();
    private readonly ScrollContainer settingsScroll = new();
    private readonly LineEdit worldUrlInput = new();
    private readonly Button connectButton = new();
    private readonly Button pairAgainButton = new();
    private readonly PanelContainer cognitionSettingsPanel = new();
    private readonly OptionButton cognitionRoleChoice = new();
    private readonly OptionButton cognitionTargetChoice = new();
    private readonly OptionButton cognitionProviderChoice = new();
    private readonly OptionButton cognitionCredentialChoice = new();
    private readonly LineEdit cognitionModelInput = new();
    private readonly LineEdit cognitionApiKeyInput = new();
    private readonly LineEdit cognitionCredentialLabelInput = new();
    private readonly Label cognitionConfigurationStatus = new();
    private readonly Label cognitionCredentialHint = new();
    private readonly Label usageMeterStatus = new();
    private readonly LineEdit usageAttemptLimitInput = new();
    private readonly Button applyUsageLimitButton = new();
    private readonly Button grantUsageCallsButton = new();
    private readonly Button refreshUsageButton = new();
    private readonly Button saveCognitionProviderButton = new();
    private readonly Button forgetCognitionCredentialButton = new();
    private readonly Button deleteCognitionCredentialSlotButton = new();
    private readonly Button refreshCognitionProviderButton = new();
    private readonly PanelContainer pairingPanel = new();
    private readonly Label pairingInstructionLabel = new();
    private readonly Label pairingCodeLabel = new();
    private readonly Label pairingIdLabel = new();
    private readonly Label pairingExpiryLabel = new();
    private readonly Button pairButton = new();
    private readonly Button forgetRegistrationButton = new();

    private readonly HBoxContainer topBar = new();
    private readonly Button mapButton = new();
    private readonly Button worldInfoButton = new();
    private readonly Label clockLabel = new();
    private readonly Label climateLabel = new();
    private readonly Button inhabitantsButton = new();
    private readonly Button eventsButton = new();
    private readonly Button settlementButton = new();
    private readonly Button menuButton = new();
    private readonly WorldTerrainLayer terrainLayer = new();
    private WorldTerrainMap? terrainMap;
    private string? terrainWorldId;
    private string? terrainManifestDigest;
    private string? terrainLayersDigest;
    private readonly Control mapCanvas = new();
    private readonly Control mapStage = new();
    private readonly PanelContainer worldOverviewPanel = new();
    private readonly WorldOverview worldOverview = new();
    private readonly Control objectLayer = new();
    private readonly Control entityLayer = new();
    private readonly Label rosterSummaryLabel = new();
    private readonly PanelContainer selectedInhabitantCard = new();
    private readonly VBoxContainer selectedAgentOverview = new();
    private readonly ScrollContainer selectedAgentModelScroll = new();
    private readonly VBoxContainer selectedAgentModelContent = new();
    private readonly Button modelSettingsButton = new();
    private readonly Label selectedActorNameLabel = new();
    private readonly LineEdit renameAgentInput = new();
    private readonly Button renameAgentButton = new();
    private readonly Label selectedActorSummaryLabel = new();
    private readonly Label selectedActorConditionLabel = new();
    private readonly Button clearSelectionButton = new();
    private readonly Button familyTreeButton = new();
    private readonly PanelContainer familyTreePanel = new();
    private readonly FamilyTreeView familyTreeView = new();
    private readonly Label familyTreeStatus = new();
    private readonly ItemList inhabitantList = new();
    private readonly RichTextLabel inhabitantDetails = new();
    private readonly RichTextLabel inhabitantSocialDetails = new();
    private readonly RichTextLabel privateThoughtHistory = new();
    private readonly Button memoriesButton = new();
    private readonly PanelContainer memoriesPanel = new();
    private readonly RichTextLabel memoryHistory = new();
    private readonly RichTextLabel worldDetails = new();
    private readonly RichTextLabel worldInfoText = new();
    private readonly RichTextLabel eventLog = new();
    private readonly PanelContainer rosterPanel = new();
    private readonly PanelContainer eventsPanel = new();
    private readonly PanelContainer settlementPanel = new();
    private readonly PanelContainer worldInfoPanel = new();
    private readonly PanelContainer selectedTilePanel = new();
    private readonly RichTextLabel selectedTileText = new();
    private Vector2I? selectedTile;
    private readonly PanelContainer gameMenuPanel = new();
    private readonly PanelContainer settingsPanel = new();
    private readonly ColorRect menuShade = new();
    private readonly Label menuHeadingLabel = new();
    private readonly Button menuResumeButton = new();
    private readonly Button quitGameButton = new();
    private readonly ConfirmationDialog quitGameConfirmation = new();
    private readonly CheckBox fullscreenToggle = new();
    private readonly OptionButton windowSizeChoice = new();
    private readonly OptionButton renderResolutionChoice = new();
    private static readonly Vector2I[] DisplaySizePresets =
    [
        new(1280, 720),
        new(1600, 900),
        new(1920, 1080),
    ];
    private readonly OptionButton clockFormatChoice = new();
    private readonly OptionButton dateFormatChoice = new();
    private readonly OptionButton lifePaceChoice = new();
    private readonly CheckBox jevAssistanceToggle = new();
    private readonly Button applyLifePaceButton = new();
    private int? lastObservedLifePace;
    private string? lastLifePaceWorldId;

    private readonly Button pauseButton = new();
    private readonly OptionButton instructionKind = new();
    private readonly LineEdit instructionText = new();
    private readonly Button submitInstructionButton = new();
    private readonly Label pendingSubmissionLabel = new();
    private readonly Button retryPendingSubmissionButton = new();
    private readonly Button forgetPendingSubmissionButton = new();
    private readonly OptionButton authoringKind = new();
    private readonly LineEdit authoringId = new();
    private readonly LineEdit authoringValue = new();
    private readonly LineEdit authoringSecondaryValue = new();
    private readonly SpinBox authoringX = new();
    private readonly SpinBox authoringY = new();
    private readonly CheckBox authoringRenewable = new();
    private readonly Button submitAuthoringButton = new();
    private readonly Label authoringHintLabel = new();
    private readonly LineEdit pairingApprovalId = new();
    private readonly LineEdit pairingApprovalCode = new();
    private readonly Button approvePairingButton = new();
    private readonly Button refreshDevicesButton = new();
    private readonly ItemList pairedDeviceList = new();
    private readonly LineEdit revokeDeviceId = new();
    private readonly Button revokeDeviceButton = new();
    private readonly Button developerToggleButton = new();
    private readonly ScrollContainer developerScroll = new();
    private readonly VBoxContainer developerBody = new();

    private OwnerDeviceKey? deviceKey;
    private OwnerDeviceRegistration? registration;
    private OwnerPairingStart? pendingPairing;
    private Uri? pendingPairingOrigin;
    private OwnerDevice[] pairedDevices = [];
    private OwnerProviderConfigurationStatus? providerConfiguration;
    private OwnerUsageStatus? usageStatus;
    private string? usagePauseWorldId;
    private bool wasObservedPaused;
    private OwnerPendingSubmission? pendingSubmission;
    private string? selectedInhabitantId;
    private string? renamingAgentId;
    private bool isRefreshing;
    private int successfulRefreshCount;
    private bool isPairingOperation;
    private bool isOwnerAction;
    private bool registeredEndpointInvalid;
    private bool menuPausedWorld;
    private OwnerWorldSnapshot? renderedMapSnapshot;
    private int currentTileSize = DefaultTileSize;
    private float cameraZoom = 1;
    private Vector2 cameraCenterTiles;
    private string? cameraWorldId;
    private bool draggingMap;
    private GameDisplayPreferences displayPreferences = new();
    private OwnerWorldCalendarPace? observedCalendarPace;

    public Main()
    {
        ownerApi = new OwnerWorldApi(httpClient);
    }

    public override void _Ready()
    {
        displayPreferences = displayPreferencesStore.Load();
        ApplySavedDisplaySettings();
        BuildLayout();
        ShowMainMenu();
        if (OS.GetCmdlineUserArgs().Contains("--ui-smoke-test", StringComparer.Ordinal))
        {
            _ = VerifyMenuLayoutAsync();
            return;
        }
        _ = TryGetCommandLineWorldUrl(out var commandLineUrl);
        worldUrlInput.Text = commandLineUrl ?? ConfiguredWorldUrl();
        _ = InitializeAsync();

        var timer = new Godot.Timer
        {
            WaitTime = RefreshSeconds,
            Autostart = true,
        };
        timer.Timeout += () => _ = PulseAsync();
        AddChild(timer);
    }

    private async Task VerifyMenuLayoutAsync()
    {
        try
        {
            foreach (var size in new[] { new Vector2I(1280, 720), new Vector2I(1024, 768) })
            {
                GetWindow().Size = size;
                for (var frame = 0; frame < 3; frame++)
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (!mainMenuOverlay.GetGlobalRect().Encloses(mainMenuCard.GetGlobalRect()) ||
                    mainMenuOverlay.GetGlobalRect().GetCenter().DistanceTo(mainMenuCard.GetGlobalRect().GetCenter()) > 2)
                    throw new InvalidOperationException($"Main Menu escaped its centered bounds at {size}.");
                manualSaveOverlay.Show();
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (!manualSaveOverlay.GetGlobalRect().Encloses(manualSaveCard.GetGlobalRect()) ||
                    manualSaveOverlay.GetGlobalRect().GetCenter().DistanceTo(manualSaveCard.GetGlobalRect().GetCenter()) > 2)
                    throw new InvalidOperationException($"Save/load panel escaped its centered bounds at {size}.");
                manualSaveOverlay.Hide();
                worldMenuHeading.Text = "New World";
                worldMenuStatus.Text = "Choose a seed and size. The new world opens paused at its empty camp; add four founders before starting time.";
                worldPreviewStatus.Text = "Map preview · camp at 100, 60. The world you create will use this terrain.";
                worldPreview.Show();
                worldMenuOverlay.Show();
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (!worldMenuOverlay.GetGlobalRect().Encloses(worldMenuCard.GetGlobalRect()) ||
                    worldMenuOverlay.GetGlobalRect().GetCenter().DistanceTo(worldMenuCard.GetGlobalRect().GetCenter()) > 2)
                    throw new InvalidOperationException($"World creation/selection panel escaped its centered bounds at {size}.");
                worldMenuOverlay.Hide();
                worldPreview.Hide();
            }
            OpenMainMenuSettings();
            if (!mainMenuOverlay.Visible || mainMenuCard.Visible || !gameMenuPanel.Visible || !gameSettingsContent.Visible ||
                worldSettingsCategoryButton.Visible || worldSettingsContent.Visible)
                throw new InvalidOperationException("Main Menu Settings must keep the title background and show only Game Settings.");
            settingsButton.EmitSignal(BaseButton.SignalName.Pressed);
            settingsButton.EmitSignal(BaseButton.SignalName.Pressed);
            gameSettingsCategoryButton.EmitSignal(BaseButton.SignalName.Pressed);
            if (!settingsPanel.Visible || !gameSettingsContent.Visible)
                throw new InvalidOperationException("The selected Settings category must remain open.");
            worldSettingsCategoryButton.EmitSignal(BaseButton.SignalName.Pressed);
            if (!gameMenuPanel.Visible || !gameSettingsContent.Visible || worldSettingsContent.Visible ||
                !returnToMainMenu)
                throw new InvalidOperationException("World Settings cannot be opened from the Main Menu.");
            await CloseGameMenuAsync();
            if (!mainMenuOverlay.Visible || !mainMenuCard.Visible || gameMenuPanel.Visible)
                throw new InvalidOperationException("Closing Game Settings must return to the Main Menu.");
            var displayWindow = GetWindow();
            var originalWindowSize = displayWindow.Size;
            var originalRenderSize = displayWindow.ContentScaleSize;
            var originalDisplayPreferences = displayPreferences;
            var originalWindowChoice = windowSizeChoice.Selected;
            var originalRenderChoice = renderResolutionChoice.Selected;
            try
            {
                windowSizeChoice.Select(1);
                SetWindowSize(1);
                if (displayWindow.Size != DisplaySizePresets[1] || displayWindow.ContentScaleSize != originalRenderSize)
                    throw new InvalidOperationException("Window Size must change only the physical window size.");
                renderResolutionChoice.Select(2);
                SetRenderResolution(2);
                if (displayWindow.Size != DisplaySizePresets[1] ||
                    displayWindow.ContentScaleSize != DisplaySizePresets[2] ||
                    displayWindow.ContentScaleMode != Window.ContentScaleModeEnum.Viewport)
                    throw new InvalidOperationException("Render Resolution must change only the viewport's render size.");
            }
            finally
            {
                displayWindow.Size = originalWindowSize;
                displayWindow.ContentScaleSize = originalRenderSize;
                windowSizeChoice.Select(originalWindowChoice);
                renderResolutionChoice.Select(originalRenderChoice);
                SaveDisplayPreferences(originalDisplayPreferences);
            }
            mainMenuOverlay.Hide();
            isInWorld = true;
            returnToMainMenu = false;
            SetWorldMenuActionsVisible(true);
            pairingPanel.Hide();
            developerScroll.Hide();
            gameMenuPanel.Show();
            var pauseActions = menuQuitToMainButton.GetParent<VBoxContainer>().GetChildren()
                .OfType<Button>().Where(button => button.Visible).Select(button => button.Text).ToArray();
            if (!pauseActions.SequenceEqual(new[] { "Save World", "Settings", "Mod Library", "Quit to Menu" }) ||
                gameMenuPanel.FindChildren("*", nameof(Button), recursive: true, owned: false)
                    .OfType<Button>().Any(button => button.Visible && button.Text == "Create"))
                throw new InvalidOperationException("Pause Menu must have only the four ordered actions and no player Create workbench.");
            modLibraryButton.EmitSignal(BaseButton.SignalName.Pressed);
            if (!modLibraryPanel.Visible || settingsPanel.Visible)
                throw new InvalidOperationException("Mod Library action must open the in-world package view.");
            settingsButton.EmitSignal(BaseButton.SignalName.Pressed);
            if (modLibraryPanel.Visible || !settingsPanel.Visible || !worldSettingsCategoryButton.Visible)
                throw new InvalidOperationException("Settings action must open the in-world Game/World category view.");
            developerToggleButton.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!developerScroll.Visible || settingsScroll.Visible || !settingsPanel.Visible)
                throw new InvalidOperationException("Developer controls must stay inside Settings without adding a Pause Menu action.");
            if (developerScroll.Size.X < 200 || !settingsPanel.GetGlobalRect().Encloses(developerScroll.GetGlobalRect()))
                throw new InvalidOperationException("Developer controls must have usable width inside Settings at a narrow window.");
            gameSettingsCategoryButton.EmitSignal(BaseButton.SignalName.Pressed);
            if (developerScroll.Visible || !settingsScroll.Visible || !gameSettingsContent.Visible)
                throw new InvalidOperationException("Game Settings must replace Developer tools in the same panel.");
            settingsPanel.Hide();
            menuQuitToMainButton.EmitSignal(BaseButton.SignalName.Pressed);
            if (!quitToMenuConfirmation.Visible)
                throw new InvalidOperationException("Quit to Menu must request confirmation.");
            quitToMenuConfirmation.Hide();
            foreach (var size in new[] { new Vector2I(1280, 720), new Vector2I(1920, 1080), new Vector2I(1024, 768) })
            {
                GetWindow().Size = size;
                foreach (var settingsVisible in new[] { false, true })
                {
                    foreach (var worldSpecific in settingsVisible ? new[] { false, true } : new[] { false })
                    {
                        if (settingsVisible)
                        {
                            ShowSettingsSection(worldSpecific);
                            if (gameSettingsContent.Visible == worldSpecific || worldSettingsContent.Visible != worldSpecific)
                                throw new InvalidOperationException("Game and World Settings must show different controls.");
                            (worldSpecific ? worldSettingsCategoryButton : gameSettingsCategoryButton)
                                .EmitSignal(BaseButton.SignalName.Pressed);
                            if (!settingsPanel.Visible || gameSettingsContent.Visible == worldSpecific ||
                                worldSettingsContent.Visible != worldSpecific)
                                throw new InvalidOperationException("Re-selecting a Settings category must leave its page open.");
                        }
                        else settingsPanel.Hide();
                        foreach (var selected in new[] { false, true, false })
                        {
                            selectedInhabitantCard.Visible = selected;
                            for (var frame = 0; frame < 5; frame++)
                            {
                                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                            }
                            ApplyResponsiveLayout();
                            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                            var menu = gameMenuPanel.GetGlobalRect();
                            var bounds = gameMenuPanel.GetParent<Control>().GetGlobalRect();
                            if (menu.GetCenter().DistanceTo(bounds.GetCenter()) > 2 || !bounds.Encloses(menu))
                            {
                                throw new InvalidOperationException($"Menu escaped its centered bounds: window={size}, settings={settingsVisible}, world={worldSpecific}, selected={selected}, menu={menu}, bounds={bounds}");
                            }
                            settlementPanel.Show();
                            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                            if (!mapCanvas.GetGlobalRect().Encloses(settlementPanel.GetGlobalRect()))
                            {
                                throw new InvalidOperationException($"Settlement panel escaped the world viewport: window={size}");
                            }
                            settlementPanel.Hide();
                            worldInfoPanel.Show();
                            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                            if (!mapCanvas.GetGlobalRect().Encloses(worldInfoPanel.GetGlobalRect()))
                                throw new InvalidOperationException($"World Info escaped the world viewport: window={size}");
                            worldInfoPanel.Hide();
                        }
                    }
                }
            }
            gameMenuPanel.Hide();
            menuShade.Hide();
            selectedInhabitantCard.Hide();
            var sampleResource = new OwnerWorldResource("wood", "construction", new(1, 1), false, "available", 8, 12, 0, 0, "spring");
            var sample = new OwnerWorldSnapshot("ui-test", 0, "ui-map", Enumerable.Range(0, 16)
                .Select(index => new OwnerWorldTile(index % 4, index / 4, "meadow")).ToArray(), [], [sampleResource], null, 0)
            {
                PackedTerrain = new OwnerWorldPackedTerrain(4, 4, "terrain-kind-v1",
                    Convert.ToBase64String(new byte[16])),
                PackedMapLayers = new OwnerWorldPackedMapLayers(4, 4, "map-layers-v1",
                    Convert.ToBase64String(Enumerable.Repeat((byte)2, 16).ToArray()),
                    Convert.ToBase64String(Enumerable.Repeat((byte)123, 16).ToArray()),
                    Convert.ToBase64String(new byte[16]),
                    Convert.ToBase64String(Enumerable.Repeat((byte)1, 16).ToArray()),
                    Convert.ToBase64String(Enumerable.Repeat((byte)3, 16).ToArray())),
                PlacedBuildings = [new("test-hall", "test-definition", new(0, 2), 0, "Test hall", ["shelter"], 2, 1)],
                ContentPackages = [new("owner-building-ui-test", "1.0.0", "sha256:test", "proposed", null, null, null, null,
                    "sha256:manifest", "Mira's shelter study", "builder-test")],
            };
            Render(sample with { WorldTick = 3_600, CalendarPace = new OwnerWorldCalendarPace(360, 40) }, []);
            if (clockLabel.Text != "01-02-0001 · 00:00" ||
                !worldInfoText.Text.Contains("40 days/year", StringComparison.Ordinal))
                throw new InvalidOperationException("The HUD must use the world's saved calendar pace, not a hard-coded day length.");
            Render(sample with { JevEnabled = true }, []);
            if (!jevAssistanceToggle.ButtonPressed)
                throw new InvalidOperationException("World Settings must reflect this world's saved Jev assistance choice.");
            Render(sample with { JevEnabled = false }, []);
            if (jevAssistanceToggle.ButtonPressed)
                throw new InvalidOperationException("World Settings must show when Jev assistance is off.");
            usageStatus = new OwnerUsageStatus(2, 1, 0, 1, 10, 3, 2, true,
                [new OwnerUsageRow("openai", "test-model", "planning", 2, 1, 0, 1, 10, 3)]);
            RenderUsageStatus();
            if (!usageMeterStatus.Text.Contains("Installation lifetime", StringComparison.Ordinal) ||
                !usageMeterStatus.Text.Contains("LIMIT REACHED", StringComparison.Ordinal) ||
                !usageMeterStatus.Text.Contains("openai / test-model", StringComparison.Ordinal) ||
                !grantUsageCallsButton.Visible || usageAttemptLimitInput.Text != "2")
                throw new InvalidOperationException("World Settings must present paid attempts, scope, provider/model and explicit consent at the cap.");
            usageStatus = null;
            RenderUsageStatus();
            Render(sample, []);
            Render(sample with { FounderSetup = new OwnerFounderSetup(4, 2, false) }, []);
            founderSetupPanel.Show();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!founderSetupButton.Visible || !founderSetupButton.Text.Contains("2/4", StringComparison.Ordinal) ||
                !startWorldButton.Visible || !startWorldButton.Disabled ||
                !mapCanvas.GetGlobalRect().Encloses(founderSetupPanel.GetGlobalRect()))
                throw new InvalidOperationException("Founder setup must show progress and keep Start World gated inside the world view.");
            founderSetupPanel.Hide();
            Render(sample with { FounderSetup = new OwnerFounderSetup(4, 4, true) }, []);
            if (!addAgentButton.Visible || founderSetupButton.Visible || startWorldButton.Visible)
                throw new InvalidOperationException("Started worlds must offer Add Agent instead of founder setup controls.");
            Render(sample, []);
            RenderModLibrary(sample);
            if (!modLibraryContents.Text.Contains("proposed by builder-test", StringComparison.Ordinal))
                throw new InvalidOperationException("Mod Library must show existing agent proposal provenance.");
            RenderMap(sample);
            for (var frame = 0; frame < 3; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var inspectClick = mapStage.Position + new Vector2(currentTileSize * 1.5f,
                currentTileSize * 1.5f);
            HandleMapInput(new InputEventMouseButton
            {
                Position = inspectClick,
                ButtonIndex = MouseButton.Left,
                Pressed = true,
            });
            if (!selectedTilePanel.Visible || terrainLayer.SelectedTile != new Vector2I(1, 1) ||
                !selectedTileText.Text.Contains("8 available", StringComparison.Ordinal) ||
                !selectedTileText.Text.Contains("Climate: Temperate", StringComparison.Ordinal) ||
                !selectedTileText.Text.Contains("Elevation: 123/255", StringComparison.Ordinal) ||
                !selectedTileText.Text.Contains("Hydrology: Land", StringComparison.Ordinal) ||
                !selectedTileText.Text.Contains("Terrain kind: Meadow", StringComparison.Ordinal) ||
                !selectedTileText.Text.Contains("Surface: Sand", StringComparison.Ordinal) ||
                !selectedTileText.Text.Contains("Vegetation: Scrub", StringComparison.Ordinal) ||
                !selectedTileText.Text.Contains("Fertility: unavailable", StringComparison.Ordinal))
                throw new InvalidOperationException("Selected-tile inspection must show separate map facts and mark unavailable fertility honestly.");
            var renderedSurface = terrainMap?.DisplayColorAt(1, 1) ?? Colors.Transparent;
            var expectedSurface = new Color("AA985F");
            if (terrainMap?.SurfaceAt(1, 1) != 1 ||
                Math.Abs(renderedSurface.R - expectedSurface.R) > 0.001f ||
                Math.Abs(renderedSurface.G - expectedSurface.G) > 0.001f ||
                Math.Abs(renderedSurface.B - expectedSurface.B) > 0.001f)
                throw new InvalidOperationException("The rendered map must use its separate surface and vegetation layers.");
            var marker = mapObjectVisuals["resource:wood"];
            var identity = marker.GetInstanceId();
            var entered = false;
            marker.MouseEntered += () => entered = true;
            GetViewport().PushInput(new InputEventMouseMotion { Position = marker.GetGlobalRect().GetCenter(), GlobalPosition = marker.GetGlobalRect().GetCenter() }, inLocalCoords: true);
            for (var frame = 0; frame < 3; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            RenderMap(sample with { Resources = [sampleResource with { Quantity = 7 }] });
            if (!selectedTileText.Text.Contains("7 available", StringComparison.Ordinal))
                throw new InvalidOperationException("Selected-tile resource stock must refresh with observations.");
            ClearTileSelection();
            if (!entered || marker.MouseFilter == MouseFilterEnum.Ignore || marker.GetInstanceId() != identity ||
                !marker.TooltipText.Contains("7/12", StringComparison.Ordinal) || !marker.Text.Contains("7/12", StringComparison.Ordinal))
                throw new InvalidOperationException($"Resource hover/update failed: entered={entered}, filter={marker.MouseFilter}, stable={marker.GetInstanceId() == identity}, text={marker.Text}, rect={marker.GetGlobalRect()}, hovered={GetViewport().GuiGetHoveredControl()?.GetPath()}.");
            var sampleTree = new OwnerWorldResource("sample-tree", "construction", new(3, 1), true,
                "available", 1, 1, 1, 6, "spring", "broadleaf");
            RenderMap(sample with { Resources = [sampleResource, sampleTree] });
            if (terrainLayer.TreeStageAt(3, 1) != "mature" || mapObjectVisuals.ContainsKey("resource:sample-tree"))
                throw new InvalidOperationException("A live tree must render as a terrain object, not a resource text label.");
            RenderMap(sample with { Resources = [sampleResource, sampleTree with { Quantity = 0, State = "depleted" }] });
            if (terrainLayer.TreeStageAt(3, 1) != "stump")
                throw new InvalidOperationException("Harvested trees must become visible stumps.");
            RenderMap(sample with { Resources = [sampleResource, sampleTree with { Quantity = 0, State = "depleted", IsPlanted = true }] });
            if (terrainLayer.TreeStageAt(3, 1) != "sapling")
                throw new InvalidOperationException("Replanted trees must become visible saplings.");
            var sampleOrchard = new OwnerWorldResource("sample-orchard", "fruit", new(2, 1), true,
                "available", 1, 1, 1, 3, "spring", "orchard", TreeStage: "fruiting");
            RenderMap(sample with { Resources = [sampleResource, sampleTree, sampleOrchard] });
            if (terrainLayer.TreeStageAt(2, 1) != "fruiting" || mapObjectVisuals.ContainsKey("resource:sample-orchard"))
                throw new InvalidOperationException("Orchard fruit must render on one tree tile instead of a resource label.");
            RenderMap(sample with { Resources = [sampleResource, sampleTree, sampleOrchard with { Quantity = 0, TreeStage = "picked" }] });
            if (terrainLayer.TreeStageAt(2, 1) != "picked")
                throw new InvalidOperationException("Picked orchard trees must lose their visible fruit.");
            RenderMap(sample with { Resources = [sampleResource, sampleTree, sampleOrchard with { Quantity = 0, TreeStage = "growing" }] });
            if (terrainLayer.TreeStageAt(2, 1) != "growing")
                throw new InvalidOperationException("Regrowing orchard trees must show their growing stage.");
            var builtMarker = mapObjectVisuals["building:test-hall"];
            if (!builtMarker.Text.Contains("Test hall", StringComparison.Ordinal) || builtMarker.Size.X <= builtMarker.Size.Y)
                throw new InvalidOperationException("Built structures must render their name and multi-tile footprint.");
            var founderPosition = new OwnerWorldPosition(2, 0);
            var founder = new OwnerWorldInhabitant("founder-ui-test", "Rowan", "active", founderPosition,
                8_000, [], [], new OwnerWorldRoute("idle", null, null, [], string.Empty),
                new OwnerWorldSpatialKnowledge(founderPosition, [founderPosition], [founderPosition]), false)
            {
                Survival = new OwnerWorldSurvival(8_200, 300, true, false, 7_400, null),
            };
            var occupied = sample with { Inhabitants = [founder] };
            RenderMap(occupied);
            var founderButton = inhabitantVisuals[founder.Id];
            var founderButtonIdentity = founderButton.GetInstanceId();
            selectedInhabitantId = founder.Id;
            RenderSelectedInhabitantCard(occupied);
            RenderMap(occupied with { WorldTick = 1 });
            RenderSelectedInhabitantCard(occupied with { WorldTick = 1 });
            for (var frame = 0; frame < 2; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (inhabitantVisuals[founder.Id].GetInstanceId() != founderButtonIdentity ||
                !selectedActorConditionLabel.Text.Contains("Warmth 82%", StringComparison.Ordinal) ||
                !selectedActorConditionLabel.IsVisibleInTree() ||
                !selectedInhabitantCard.GetGlobalRect().Encloses(selectedActorConditionLabel.GetGlobalRect()))
                throw new InvalidOperationException("Agent hover targets and condition stats must survive observation refreshes.");
            UpdateTileHover(founderButton.Position + mapStage.Position + founderButton.Size / 2);
            if (terrainLayer.HoveredTile is not null)
                throw new InvalidOperationException("An agent marker must take hover priority over its ground tile.");
            UpdateTileHover(new Vector2(currentTileSize * 1.5f, currentTileSize * 0.5f) + mapStage.Position);
            if (terrainLayer.HoveredTile != new Vector2I(1, 0))
                throw new InvalidOperationException("The hovered ground tile must receive a square outline.");
            selectedInhabitantId = null;
            selectedInhabitantCard.Hide();
            var agentClick = founderButton.GetGlobalRect().GetCenter();
            GetViewport().PushInput(new InputEventMouseButton
            {
                Position = agentClick,
                GlobalPosition = agentClick,
                ButtonIndex = MouseButton.Left,
                Pressed = true,
            }, inLocalCoords: true);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (selectedInhabitantId != founder.Id)
                throw new InvalidOperationException("Clicking an agent marker must select the agent before the ground tile.");
            selectedInhabitantId = null;
            RenderMap(sample with { Resources = [], PlacedBuildings = [] });
            if (inhabitantVisuals.ContainsKey(founder.Id))
                throw new InvalidOperationException("Removed agent marker was retained.");
            if (mapObjectVisuals.ContainsKey("resource:wood")) throw new InvalidOperationException("Removed resource marker was retained.");
            if (mapObjectVisuals.ContainsKey("building:test-hall")) throw new InvalidOperationException("Removed building marker was retained.");
            var crowded = sample with
            {
                WorldId = "ui-marker-bounds",
                PackedTerrain = null,
                PackedMapLayers = null,
                MapLayersDigest = null,
                Tiles = Enumerable.Range(0, 64 * 64)
                    .Select(index => new OwnerWorldTile(index % 64, index / 64, "meadow")).ToArray(),
                Inhabitants = Enumerable.Range(0, 4)
                    .Select(index => founder with { Id = $"crowded-{index}", Position = new OwnerWorldPosition(2, 2) })
                    .ToArray(),
                Resources = [],
                PlacedBuildings = [],
            };
            foreach (var zoom in new[] { 1f, 2f, 4f })
            {
                cameraZoom = zoom;
                RenderMap(crowded);
                CenterCameraAt(new Vector2(2.5f, 2.5f));
                var tileRect = new Rect2(new Vector2(2 * currentTileSize, 2 * currentTileSize),
                    new Vector2(currentTileSize, currentTileSize));
                var markers = crowded.Inhabitants.Select(person => inhabitantVisuals[person.Id]).ToArray();
                if (markers.Any(marker => !tileRect.Encloses(new Rect2(marker.Position, marker.Size))) ||
                    markers.Where((marker, i) => markers.Skip(i + 1)
                        .Any(other => new Rect2(marker.Position, marker.Size).Intersects(
                            new Rect2(other.Position, other.Size)))).Any())
                    throw new InvalidOperationException($"Crowded marker hitboxes overflow or overlap at {currentTileSize}px tiles.");
                UpdateTileHover(new Vector2(3.5f * currentTileSize, 2.5f * currentTileSize) + mapStage.Position);
                if (terrainLayer.HoveredTile != new Vector2I(3, 2))
                    throw new InvalidOperationException("An adjacent tile must not hit a crowded agent marker.");
            }
            RenderMap(sample with { Resources = [], PlacedBuildings = [] });
            var smallMapTileSize = currentTileSize;
            HandleMapInput(new InputEventMouseButton { ButtonIndex = MouseButton.WheelUp, Pressed = true });
            if (currentTileSize <= smallMapTileSize)
                throw new InvalidOperationException("Mouse-wheel zoom must work even when the small starter map reaches its fitted tile-size cap.");
            RenderMap(sample with
            {
                WorldId = "ui-navigation",
                PackedTerrain = null,
                PackedMapLayers = null,
                MapLayersDigest = null,
                Tiles = Enumerable.Range(0, 192)
                    .Select(index => new OwnerWorldTile(index % 16, index / 16, "meadow")).ToArray(),
                Resources = [],
                PlacedBuildings = [],
            });
            mapButton.EmitSignal(BaseButton.SignalName.Pressed);
            for (var frame = 0; frame < 2; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!worldOverviewPanel.Visible || worldOverview.VisibleTiles.Size.Y <= 0)
                throw new InvalidOperationException("The top-left map button did not open a camera-aware world overview.");
            var fittedTileSize = currentTileSize;
            var fittedViewHeight = worldOverview.VisibleTiles.Size.Y;
            for (var index = 0; index < 2; index++)
                HandleMapInput(new InputEventMouseButton { ButtonIndex = MouseButton.WheelUp, Pressed = true });
            if (currentTileSize <= fittedTileSize || worldOverview.VisibleTiles.Size.Y >= fittedViewHeight)
                throw new InvalidOperationException($"Mouse-wheel zoom did not narrow the visible world area: tile={fittedTileSize}->{currentTileSize}, view={fittedViewHeight}->{worldOverview.VisibleTiles.Size.Y}.");
            var beforeOverviewClick = mapStage.Position;
            worldOverview._GuiInput(new InputEventMouseButton
            {
                ButtonIndex = MouseButton.Left,
                Pressed = true,
                Position = new Vector2(worldOverview.Size.X / 2, 12),
            });
            if (mapStage.Position.DistanceTo(beforeOverviewClick) < 1)
                throw new InvalidOperationException("Clicking the overview did not move the world camera.");
            var beforeOverviewDrag = mapStage.Position;
            worldOverview._GuiInput(new InputEventMouseMotion
            {
                Position = new Vector2(worldOverview.Size.X / 2, worldOverview.Size.Y - 12),
            });
            worldOverview._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false });
            if (mapStage.Position.DistanceTo(beforeOverviewDrag) < 1)
                throw new InvalidOperationException("Dragging the overview did not move the world camera.");
            var beforeMiddleDrag = mapStage.Position;
            HandleMapInput(new InputEventMouseButton { ButtonIndex = MouseButton.Middle, Pressed = true });
            HandleMapInput(new InputEventMouseMotion { Relative = new Vector2(0, 60) });
            HandleMapInput(new InputEventMouseButton { ButtonIndex = MouseButton.Middle, Pressed = false });
            if (mapStage.Position.DistanceTo(beforeMiddleDrag) < 1)
                throw new InvalidOperationException("Middle-drag did not pan the world camera.");
            GetViewport().GuiGetFocusOwner()?.ReleaseFocus();
            var beforeKeyboardPan = mapStage.Position;
            _UnhandledKeyInput(new InputEventKey { Keycode = Key.S, Pressed = true });
            if (mapStage.Position.DistanceTo(beforeKeyboardPan) < 1)
                throw new InvalidOperationException("Keyboard panning did not move the world camera.");
            var eventDestination = cameraCenterTiles.X < 8 ? new OwnerWorldPosition(15, 11) : new OwnerWorldPosition(0, 0);
            knownEvents[100] = new OwnerWorldEvent(100, 1, "food_consumed", "founder-scout", eventDestination);
            RenderEventLog();
            var beforeEventJump = cameraCenterTiles;
            eventLog.EmitSignal(RichTextLabel.SignalName.MetaClicked, "100");
            if (cameraCenterTiles.DistanceTo(beforeEventJump) < 0.5f)
                throw new InvalidOperationException("Clicking a located event did not move the world camera.");
            var largeTerrain = Enumerable.Range(0, 256 * 128)
                .Select(index => (byte)(index % 37 == 0 ? 3 : 0)).ToArray();
            var largeMap = sample with
            {
                WorldId = "ui-large-map",
                MapManifestDigest = "ui-large-map-v1",
                Tiles = [],
                PackedTerrain = new OwnerWorldPackedTerrain(256, 128, "terrain-kind-v1",
                    Convert.ToBase64String(largeTerrain)),
                PackedMapLayers = null,
                MapLayersDigest = null,
                Authoring = new OwnerWorldAuthoringState(true, 0, 0, 0, "ui-large-map-v1",
                    "ui-large-map-v1", "clear", "spring", []),
                WeatherRegionSize = 32,
                WeatherRegions = Enumerable.Range(0, 4)
                    .SelectMany(x => Enumerable.Range(0, 2).Select(y => new OwnerWeatherRegion(x, y, "snow", 12)))
                    .Append(new OwnerWeatherRegion(4, 2, "rain", 78)).ToArray(),
                Resources = [],
                PlacedBuildings = [],
            };
            RenderMap(largeMap);
            for (var frame = 0; frame < 2; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (terrainLayer.GetChildCount() != 0 || terrainLayer.VisibleTileCount >= largeTerrain.Length / 2 ||
                worldOverview.VisibleTiles.Size.X >= 256)
                throw new InvalidOperationException($"A regional map must draw only the visible terrain without per-tile nodes: children={terrainLayer.GetChildCount()}, visible={terrainLayer.VisibleTileCount}, overview={worldOverview.VisibleTiles.Size}.");
            if (climateLabel.Text != "Spring · Rain" ||
                !worldInfoText.Text.Contains("Soil moisture nearby: 78/100", StringComparison.Ordinal) ||
                terrainLayer.WeatherAt(150, 80) != "rain" || terrainLayer.WeatherAt(20, 20) != "snow")
                throw new InvalidOperationException("The world HUD and info must show weather and moisture at the camera.");
            var beforeLargePan = worldOverview.VisibleTiles.Position;
            CenterCameraAt(new Vector2(20, 20));
            if (worldOverview.VisibleTiles.Position.DistanceTo(beforeLargePan) < 1 ||
                terrainLayer.VisibleTileCount >= largeTerrain.Length / 2)
                throw new InvalidOperationException("Panning a large map must update the camera-bounded terrain view.");
            if (climateLabel.Text != "Spring · Snow" ||
                !worldInfoText.Text.Contains("camera: Spring · Snow", StringComparison.Ordinal) ||
                !worldInfoText.Text.Contains("Soil moisture nearby: 12/100", StringComparison.Ordinal))
                throw new InvalidOperationException($"Panning must update HUD and World Info to local weather: camera={cameraCenterTiles}, HUD={climateLabel.Text}, info={worldInfoText.Text}.");
            var wrappedMap = largeMap with
            {
                WorldId = "ui-wrapped-map",
                WrapsEastWest = true,
                WeatherRegions = [.. largeMap.WeatherRegions, new OwnerWeatherRegion(7, 2, "storm", 40)],
                Resources = [new OwnerWorldResource("seam-wood", "construction", new(255, 64),
                    true, "available", 5, 10, 0, 0, "spring")],
            };
            RenderMap(wrappedMap);
            CenterCameraAt(new Vector2(0.5f, 64));
            if (worldOverview.VisibleTiles.Position.X >= 0 ||
                terrainLayer.VisibleTileCount >= largeTerrain.Length / 2 ||
                !worldOverview.WrapsEastWest ||
                terrainLayer.WeatherAt(-1, 70) != "storm" ||
                TileAtCanvas(mapCanvas.Size / 2 - new Vector2(2 * currentTileSize, 0), wrappedMap).X != 254)
                throw new InvalidOperationException("Wrapped camera must render and target the western seam without an empty edge.");
            var seamMarker = mapObjectVisuals["resource:seam-wood"];
            if (!mapCanvas.GetGlobalRect().HasPoint(seamMarker.GetGlobalRect().GetCenter()))
                throw new InvalidOperationException("Resources across the wrapped seam must remain visible at the camera.");
            var seamEntered = false;
            seamMarker.MouseEntered += () => seamEntered = true;
            GetViewport().PushInput(new InputEventMouseMotion
            {
                Position = seamMarker.GetGlobalRect().GetCenter(),
                GlobalPosition = seamMarker.GetGlobalRect().GetCenter(),
            }, inLocalCoords: true);
            for (var frame = 0; frame < 2; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!seamEntered)
                throw new InvalidOperationException("A resource across the wrapped seam must remain hoverable.");
            PanCamera(new Vector2(-5, 0));
            if (cameraCenterTiles.X < 250 || worldOverview.VisibleTiles.End.X <= 256 ||
                !mapCanvas.GetGlobalRect().HasPoint(seamMarker.GetGlobalRect().GetCenter()))
                throw new InvalidOperationException("Panning west across a wrapped seam must not clamp the camera.");
            PanCamera(new Vector2(10, 0));
            if (cameraCenterTiles.X > 10 || worldOverview.VisibleTiles.Position.X >= 0 ||
                !mapCanvas.GetGlobalRect().HasPoint(seamMarker.GetGlobalRect().GetCenter()))
                throw new InvalidOperationException("Panning east across a wrapped seam must remain continuous.");
            RenderMap(largeMap);
            CenterCameraAt(new Vector2(-5, 64));
            if (worldOverview.VisibleTiles.Position.X < 0 || worldOverview.WrapsEastWest)
                throw new InvalidOperationException("Non-wrapped worlds must retain bounded horizontal camera edges.");

            cameraZoom = 1;
            RenderMap(largeMap);
            var oldVisibleWidth = worldOverview.VisibleTiles.Size.X;
            var oldTileCount = terrainLayer.VisibleTileCount;
            var baselinePan = System.Diagnostics.Stopwatch.StartNew();
            for (var step = 0; step < 8; step++)
            {
                CenterCameraAt(new Vector2(80 + step, 64));
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            baselinePan.Stop();
            cameraZoom = 0.65f;
            RenderMap(largeMap);
            if (currentTileSize != 8 || worldOverview.VisibleTiles.Size.X < oldVisibleWidth * 1.4f ||
                terrainLayer.VisibleTileCount > 40_000)
                throw new InvalidOperationException($"Overview zoom must widen bounded terrain coverage: tile={currentTileSize}, width={oldVisibleWidth}->{worldOverview.VisibleTiles.Size.X}, tiles={terrainLayer.VisibleTileCount}.");
            var wideVisibleWidth = worldOverview.VisibleTiles.Size.X;
            var wideTileCount = terrainLayer.VisibleTileCount;
            var widePan = System.Diagnostics.Stopwatch.StartNew();
            for (var step = 0; step < 8; step++)
            {
                CenterCameraAt(new Vector2(80 + step, 64));
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            widePan.Stop();
            GD.Print($"Zoom comparison at {mapCanvas.Size}: 12 px={oldVisibleWidth:0} columns/{oldTileCount} tiles, {baselinePan.Elapsed.TotalMilliseconds:0} ms/8 pan frames; 8 px={wideVisibleWidth:0} columns/{wideTileCount} tiles, {widePan.Elapsed.TotalMilliseconds:0} ms/8 pan frames (headless sample).");
            cameraZoom = 1;
            RenderMap(largeMap);
            var formerPosition = new OwnerWorldPosition(2, 2);
            var deceased = new OwnerWorldInhabitant("archived-mira", "Mira", "dead", formerPosition,
                5_000, [], [new("age-band", "elder"), new("death-tick", "1")],
                new OwnerWorldRoute("deceased", null, null, [], string.Empty),
                new OwnerWorldSpatialKnowledge(formerPosition, [formerPosition], [formerPosition]), false)
            {
                RecentPrivateThoughts = [new OwnerWorldPrivateThought(1, "I hope Rowan remembers our garden.")],
                RecentMemories = [new OwnerWorldAgentMemory(1, "living-parent", "Rowan",
                    "I hid the garden tools where Rowan cannot see them.", "private")],
            };
            var historicalSnapshot = sample with
            {
                WorldId = "ui-deceased",
                Inhabitants = [deceased],
                Resources = [],
                PlacedBuildings = [],
            };
            RenderMap(historicalSnapshot);
            RenderInhabitantList(historicalSnapshot);
            selectedInhabitantId = deceased.Id;
            RenderSelectedInhabitantCard(historicalSnapshot);
            if (entityLayer.GetChildren().Any(child => !child.IsQueuedForDeletion()) ||
                inhabitantList.ItemCount != 1 || !rosterSummaryLabel.Text.Contains("1 deceased", StringComparison.Ordinal) ||
                !selectedInhabitantCard.Visible || !selectedActorSummaryLabel.Text.Contains("Dead", StringComparison.Ordinal) ||
                renameAgentInput.Text != "Mira")
                throw new InvalidOperationException("A deceased inhabitant must remain inspectable without appearing as a living map actor.");
            if (!privateThoughtHistory.Text.Contains("I hope Rowan remembers our garden.", StringComparison.Ordinal) ||
                !privateThoughtHistory.Text.Contains("historical", StringComparison.Ordinal))
                throw new InvalidOperationException("Deceased profiles must retain their saved private thoughts without generating new ones.");
            memoriesButton.EmitSignal(BaseButton.SignalName.Pressed);
            if (!memoriesPanel.Visible ||
                !memoryHistory.Text.Contains("I hid the garden tools", StringComparison.Ordinal) ||
                inhabitantSocialDetails.Text.Contains("I hid the garden tools", StringComparison.Ordinal))
                throw new InvalidOperationException("Historical private memories must be inspectable separately from public social notes.");
            memoriesPanel.Hide();
            cameraZoom = 4;
            RenderMap(historicalSnapshot);
            var deathDestination = cameraCenterTiles.X < 8
                ? new OwnerWorldPosition(15, 11) : new OwnerWorldPosition(0, 0);
            knownEvents[101] = new OwnerWorldEvent(101, 2, "inhabitant_removed", deceased.Id,
                deathDestination);
            RenderEventLog();
            if (!eventLog.GetParsedText().Contains("died.", StringComparison.Ordinal) ||
                DescribeWorldEvent(knownEvents[101], historicalSnapshot) != "Mira died." ||
                gameSettingsContent.GetChildren().OfType<Label>()
                    .Any(label => label.Text.Contains("event pop-ups", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Deaths must remain in the Event Log without an event pop-up setting.");
            ToggleEvents();
            if (!eventsPanel.Visible || !selectedInhabitantCard.Visible)
                throw new InvalidOperationException("The Event Log and agent info panel must remain available.");
            var beforeDeathJump = cameraCenterTiles;
            eventLog.EmitSignal(RichTextLabel.SignalName.MetaClicked, "101");
            if (eventsPanel.Visible || cameraCenterTiles.DistanceTo(beforeDeathJump) < 0.5f)
                throw new InvalidOperationException("A death in the Event Log must jump to its location.");
            var parentPosition = new OwnerWorldPosition(1, 1);
            var parent = new OwnerWorldInhabitant("living-parent", "Rowan", "active", parentPosition,
                7_000, [], [new("age-band", "adult")],
                new OwnerWorldRoute("idle", null, null, [], string.Empty),
                new OwnerWorldSpatialKnowledge(parentPosition, [parentPosition], [parentPosition]), false)
            {
                Relationships =
                [
                    new OwnerWorldInhabitantRelationship("birth:test", deceased.Id,
                        "biological_parentage", "accepted", "family", 1, "parent"),
                    new OwnerWorldInhabitantRelationship("partner:test", "living-partner",
                        "partnership", "accepted", "family", 1, "partner"),
                ],
            };
            var partner = new OwnerWorldInhabitant("living-partner", "Ilya", "active", parentPosition,
                7_000, [], [new("age-band", "adult")],
                new OwnerWorldRoute("idle", null, null, [], string.Empty),
                new OwnerWorldSpatialKnowledge(parentPosition, [parentPosition], [parentPosition]), false)
            {
                Relationships = [new OwnerWorldInhabitantRelationship("partner:test", parent.Id,
                    "partnership", "accepted", "family", 1, "partner")],
            };
            var child = deceased with
            {
                Relationships = [new OwnerWorldInhabitantRelationship("birth:test", parent.Id,
                    "biological_parentage", "accepted", "family", 1, "child")],
            };
            ShowFamilyTree(historicalSnapshot with { Inhabitants = [parent, child, partner] }, child.Id);
            if (!familyTreePanel.Visible || familyTreeView.ParentEdgeCount != 1 || familyTreeView.PartnerEdgeCount != 1 ||
                !familyTreeView.VisiblePersonIds.Contains(parent.Id) ||
                !familyTreeView.VisiblePersonIds.Contains(child.Id) ||
                !familyTreeView.VisiblePersonIds.Contains(partner.Id))
                throw new InvalidOperationException("Family tree must show ancestry, partnerships and deceased profiles.");
            for (var frame = 0; frame < 2; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            ApplyResponsiveLayout();
            if (!mapCanvas.GetGlobalRect().Encloses(familyTreePanel.GetGlobalRect()))
                throw new InvalidOperationException("Family tree panel must fit within the world view.");
            familyTreeView.GetChildren().OfType<Button>().Single(button => button.Text.StartsWith(parent.DisplayName, StringComparison.Ordinal))
                .EmitSignal(BaseButton.SignalName.Pressed);
            if (selectedInhabitantId != parent.Id || familyTreePanel.Visible)
                throw new InvalidOperationException("Selecting a relative must open that person's agent profile.");
            familyTreeView.SetPeople("roommates-only", [parent with { Relationships = [] }, child with { Relationships = [] }, partner with { Relationships = [] }], child.Id);
            if (familyTreeView.VisiblePersonIds.Count != 1 || familyTreeView.ParentEdgeCount != 0)
                throw new InvalidOperationException("Household membership must not create a family link.");
            familyTreePanel.Hide();
            quitGameButton.EmitSignal(BaseButton.SignalName.Pressed);
            if (!quitGameConfirmation.Visible)
                throw new InvalidOperationException("Quit Game must ask for confirmation before exiting.");
            quitGameConfirmation.Hide();
            GD.Print("UI checks passed: startup Main Menu and settings, compact in-world pause menu and read-only Mod Library, confirmed quit, settlement panel, resource hover, square tile hover and agent priority, bounded marker hitboxes at zoom, building footprints, camera-bounded large terrain and regional weather, zoom, middle-drag, WASD, overview navigation, Event Log jumps without pop-ups, private thoughts, memories, deceased inspection and family tree.");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError(exception.Message);
            GetTree().Quit(1);
        }
    }

    public override void _ExitTree()
    {
        deviceKey?.Dispose();
        httpClient.Dispose();
        base._ExitTree();
    }

    private async Task InitializeAsync()
    {
        try
        {
            deviceKey = OwnerDeviceKey.OpenOrCreate();
            registration = registrationStore.TryLoad(deviceKey.PublicKeyFingerprint);
            if (registration is null)
            {
                RefreshMainMenuAvailability();
                return;
            }

            // Once paired, this device is pinned to the server origin that
            // issued the registration. A command-line URL is useful only for
            // a first pairing; it must never silently retarget an owner key.
            if (!WorldServerOrigin.TryResolve(registration.WorldUrl, out var storedWorldUri))
            {
                registeredEndpointInvalid = true;
                pairingPanel.Show();
                OpenMenuForSetup();
                pairingInstructionLabel.Text = "This saved device registration has no valid pinned server endpoint. Forget the local registration, then pair this Windows key again at the intended HTTPS host.";
                SetStatus("saved owner endpoint is invalid · re-pair required", good: false);
                RefreshControlAvailability();
                return;
            }

            worldUrlInput.Text = storedWorldUri.AbsoluteUri;
            LoadPendingSubmission();

            pairingPanel.Hide();
            settingsPanel.Hide();
            CloseGameMenu();
            RefreshMainMenuAvailability();
            SetStatus("paired device loaded · continue from Main Menu", good: true);
        }
        catch (Exception exception)
        {
            pairingPanel.Show();
            OpenMenuForSetup();
            SetStatus($"owner key unavailable · {FriendlyFailure(exception)}", good: false);
            pairingInstructionLabel.Text = "This client needs the Windows current-user key store. It does not create a portable private-key file.";
            pairButton.Disabled = true;
        }
    }

    private async Task PulseAsync()
    {
        if (pendingPairing is not null)
        {
            await PollPairingAsync();
            return;
        }

        if (isInWorld && registration is not null && !registeredEndpointInvalid)
        {
            await RefreshAsync();
        }
    }

    private async Task StartPairingAsync()
    {
        if (isPairingOperation || pendingPairing is not null || deviceKey is null)
        {
            return;
        }

        isPairingOperation = true;
        RefreshControlAvailability();
        try
        {
            var origin = ResolveWorldUri();
            pendingPairing = await ownerApi.StartPairingAsync(
                origin,
                deviceKey,
                CancellationToken.None);
            pendingPairingOrigin = origin;
            pairingPanel.Show();
            pairingInstructionLabel.Text = "Give the host the pairing ID and short comparison code below. The host approves it on its private loopback listener; this code is not a password.";
            pairingCodeLabel.Text = pendingPairing.PairingCode;
            pairingIdLabel.Text = pendingPairing.PairingId;
            pairingExpiryLabel.Text = $"expires {pendingPairing.ExpiresAtUtc.LocalDateTime:yyyy-MM-dd HH:mm:ss}";
            pairButton.Text = "Start fresh pairing";
            SetStatus("waiting for private host approval", good: true);
        }
        catch (Exception exception)
        {
            SetStatus($"could not start device pairing · {FriendlyFailure(exception)}", good: false);
        }
        finally
        {
            isPairingOperation = false;
            RefreshControlAvailability();
        }
    }

    private async Task PollPairingAsync()
    {
        if (isPairingOperation || pendingPairing is null || deviceKey is null)
        {
            return;
        }

        isPairingOperation = true;
        try
        {
            var status = await ownerApi.GetPairingStatusAsync(
                ResolveWorldUri(),
                pendingPairing.PairingId,
                CancellationToken.None);
            if (!Equals(status.Authority, pendingPairing.Authority) ||
                !string.Equals(status.DeviceId, pendingPairing.DeviceId, StringComparison.Ordinal) ||
                !string.Equals(status.PublicKeyFingerprint, deviceKey.PublicKeyFingerprint, StringComparison.Ordinal))
            {
                pendingPairingOrigin = null;
                pendingPairing = null;
                SetStatus("pairing status does not match this device and server · start a fresh pairing", good: false);
                return;
            }

            switch (status.State)
            {
                case OwnerPairingState.Pending:
                    pairingInstructionLabel.Text = "Waiting for host approval. Give the host the pairing ID and comparison code exactly as shown.";
                    break;
                case OwnerPairingState.Approved:
                    pairingInstructionLabel.Text = "Host approval received. Proving possession of this Windows device key…";
                    await ActivatePendingPairingAsync();
                    break;
                case OwnerPairingState.Active:
                    // The process can be interrupted after a successful
                    // server activation but before its non-secret local
                    // registration is flushed. Recover only when the active
                    // record remains bound to this exact Windows key.
                    if (string.Equals(status.DeviceId, pendingPairing.DeviceId, StringComparison.Ordinal) &&
                        string.Equals(status.PublicKeyFingerprint, deviceKey.PublicKeyFingerprint, StringComparison.Ordinal) &&
                        Equals(status.Authority, pendingPairing.Authority))
                    {
                        registration = new OwnerDeviceRegistration(
                            status.Authority,
                            status.DeviceId,
                            deviceKey.PublicKeyFingerprint,
                            ResolveWorldUri().AbsoluteUri);
                        registrationStore.Save(registration);
                        LoadPendingSubmission();
                        pendingPairing = null;
                        pendingPairingOrigin = null;
                        pairingPanel.Hide();
                        settingsPanel.Hide();
                        CloseGameMenu();
                        SetStatus("recovered the active device registration · requesting signed owner observation", good: true);
                        await RefreshAsync();
                    }
                    else
                    {
                        pairingInstructionLabel.Text = "This pairing is active but is not bound to this device key. Revoke it at the host before attempting another pairing.";
                        SetStatus("active pairing does not match this device key", good: false);
                    }

                    break;
                case OwnerPairingState.Expired:
                    pairingInstructionLabel.Text = "The comparison code expired. Start a fresh pairing to get a new short code.";
                    pendingPairing = null;
                    pendingPairingOrigin = null;
                    SetStatus("pairing expired", good: false);
                    break;
                default:
                    SetStatus("unknown pairing state returned by server", good: false);
                    break;
            }
        }
        catch (Exception exception)
        {
            SetStatus($"pairing status unavailable · {FriendlyFailure(exception)}", good: false);
        }
        finally
        {
            isPairingOperation = false;
            RefreshControlAvailability();
        }
    }

    private async Task ActivatePendingPairingAsync()
    {
        if (pendingPairing is null || deviceKey is null)
        {
            return;
        }

        var pairing = pendingPairing;
        try
        {
            var device = await ownerApi.ActivatePairingAsync(
                ResolveWorldUri(),
                pairing,
                deviceKey,
                CancellationToken.None);
            registration = new OwnerDeviceRegistration(
                pairing.Authority,
                device.DeviceId,
                deviceKey.PublicKeyFingerprint,
                ResolveWorldUri().AbsoluteUri);
            registrationStore.Save(registration);
            LoadPendingSubmission();
            pendingPairing = null;
            pendingPairingOrigin = null;
            pairingPanel.Hide();
            settingsPanel.Hide();
            CloseGameMenu();
            ShowMainMenu();
            SetStatus("device paired · continue from Main Menu", good: true);
        }
        catch (Exception exception)
        {
            SetStatus($"host approved pairing, but key activation failed · {FriendlyFailure(exception)}", good: false);
        }
    }

    private void ForgetLocalRegistration()
    {
        registrationStore.Forget();
        registration = null;
        registeredEndpointInvalid = false;
        pendingPairing = null;
        pendingPairingOrigin = null;
        pairedDevices = [];
        providerConfiguration = null;
        pairedDeviceList.Clear();
        pendingSubmission = null;
        _ = pendingSubmissionStore.TryForget();
        RenderPendingSubmission();
        knownEvents.Clear();
        pairingPanel.Show();
        connectionPanel.Show();
        OpenMenuForSetup();
        pairingCodeLabel.Text = "—";
        pairingIdLabel.Text = "—";
        pairingExpiryLabel.Text = string.Empty;
        pairingInstructionLabel.Text = "Local public registration forgotten. The Windows private key remains in the current-user key store; start a new pairing only if the host allows that key to be paired.";
        SetStatus("local registration forgotten", good: false);
        RefreshControlAvailability();
    }

    private async Task RefreshAsync()
    {
        if (isRefreshing || registeredEndpointInvalid || registration is null || deviceKey is null)
        {
            return;
        }

        isRefreshing = true;
        try
        {
            var requestedCursor = observationSession.EventCursor;
            var cachedTerrain = observationSession.Current is { } held &&
                held.Handshake.ServerCapabilities.Contains("owner-terrain-delta.v1", StringComparer.Ordinal) &&
                held.Baseline.Snapshot.PackedTerrain is not null &&
                (held.Baseline.Snapshot.MapLayersDigest is null ||
                 held.Baseline.Snapshot.PackedMapLayers is not null)
                ? held.Baseline.Snapshot : null;
            var cachedMapLayersDigest = cachedTerrain is not null &&
                observationSession.Current!.Handshake.ServerCapabilities.Contains(
                    "owner-map-layer-delta.v1", StringComparer.Ordinal)
                ? cachedTerrain.MapLayersDigest : null;
            var reconnect = await ownerApi.ReconnectAsync(
                ResolveWorldUri(),
                registration.Authority,
                registration.DeviceId,
                requestedCursor,
                cachedTerrain?.WorldId,
                cachedTerrain?.MapManifestDigest,
                cachedMapLayersDigest,
                deviceKey,
                CancellationToken.None);
            if (!observationSession.TryAccept(reconnect, requestedCursor, out var failure))
            {
                ShowHeldState(failure);
                return;
            }

            if (reconnect.Baseline.Events.ResetRequired)
            {
                knownEvents.Clear();
            }
            Render(observationSession.Current!.Baseline.Snapshot, reconnect.Baseline.Events.Events);
            successfulRefreshCount++;
            if (!isOwnerAction)
            {
                SetStatus(usageStatus?.LimitReached == true && reconnect.Baseline.Snapshot.Authoring?.IsPaused == true
                    ? "Paid-call limit reached. The world is paused; open World Settings to allow more calls."
                    : string.Empty,
                    good: usageStatus?.LimitReached != true);
            }
        }
        catch (Exception exception)
        {
            ShowHeldState(FriendlyFailure(exception));
        }
        finally
        {
            isRefreshing = false;
            RefreshControlAvailability();
        }
    }

    private async Task SetPausedAsync(bool paused)
    {
        if (!TryGetOwner(out var authority, out var deviceId, out var signer))
        {
            return;
        }

        await RunOwnerActionAsync(async () =>
        {
            var receipt = await ownerApi.SetPausedAsync(
                ResolveWorldUri(), authority, deviceId, paused, signer, CancellationToken.None);
            return receipt.Changed
                ? $"world {receipt.Operation}d at revision {receipt.Revision}"
                : $"world was already {(paused ? "paused" : "running")}";
        });
    }

    private async Task SubmitInstructionAsync()
    {
        if (!TryGetOwner(out var authority, out var deviceId, out var signer) ||
            observationSession.Current is not { } current)
        {
            SetStatus("wait for a paired observation before sending an instruction", good: false);
            return;
        }

        var selected = current.Baseline.Snapshot.Inhabitants
            .FirstOrDefault(inhabitant => string.Equals(inhabitant.Id, selectedInhabitantId, StringComparison.Ordinal));
        if (selected is null || selected.IsDraft)
        {
            SetStatus("select an active inhabitant before sending an instruction", good: false);
            return;
        }
        if (selected.DecisionFactors.Any(factor => factor.Key == "age-band" && factor.Detail == "infant"))
        {
            SetStatus("infants need care from an adult caregiver, not work instructions", good: false);
            return;
        }

        var text = instructionText.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            SetStatus("write an instruction before sending it", good: false);
            return;
        }

        var action = new OwnerInstructionAction(
            $"instruction_{OwnerPairingProtocol.CreateRequestId()}",
            selected.Id,
            instructionKind.GetSelectedId() == 1 ? "must_do" : "suggestive",
            text);
        if (!TryBeginPendingInstruction(action, out var pending))
        {
            return;
        }

        var completed = false;
        await RunOwnerActionAsync(async () =>
        {
            var receipt = await ownerApi.SubmitInstructionAsync(
                ResolveWorldUri(), authority, deviceId, action, signer, CancellationToken.None);
            completed = true;
            instructionText.Text = string.Empty;
            return $"queued {action.Kind} instruction {receipt.InstructionId}";
        });
        if (completed)
        {
            CompletePendingSubmission(pending);
        }
    }

    private async Task SubmitAuthoringAsync()
    {
        if (!TryGetOwner(out var authority, out var deviceId, out var signer) ||
            observationSession.Current?.Baseline.Snapshot.Authoring is not { IsPaused: true })
        {
            SetStatus("paused authoring is disabled until the server reports an atomic paused boundary", good: false);
            return;
        }

        var operation = new OwnerAuthoringOperationAction(
            SelectedAuthoringKind(),
            EmptyToNull(authoringId.Text),
            EmptyToNull(authoringValue.Text),
            EmptyToNull(authoringSecondaryValue.Text),
            checked((int)authoringX.Value),
            checked((int)authoringY.Value),
            authoringRenewable.ButtonPressed);
        var batch = new OwnerAuthoringBatchAction(
            $"authoring_{OwnerPairingProtocol.CreateRequestId()}",
            [operation]);
        if (!TryBeginPendingAuthoring(batch, out var pending))
        {
            return;
        }

        var completed = false;
        await RunOwnerActionAsync(async () =>
        {
            var receipt = await ownerApi.SubmitAuthoringAsync(
                ResolveWorldUri(), authority, deviceId, batch, signer, CancellationToken.None);
            completed = true;
            return receipt.Applied
                ? $"applied {operation.Kind} at revision {receipt.Revision}"
                : $"authoring rejected · {receipt.Failure ?? "unknown validation failure"}";
        });
        if (completed)
        {
            CompletePendingSubmission(pending);
        }
    }

    private async Task ApprovePairingAsync()
    {
        if (!TryGetOwner(out var authority, out var deviceId, out var signer))
        {
            return;
        }

        var pairingId = pairingApprovalId.Text.Trim();
        var pairingCode = pairingApprovalCode.Text.Trim();
        if (string.IsNullOrWhiteSpace(pairingId) || string.IsNullOrWhiteSpace(pairingCode))
        {
            SetStatus("enter the pending pairing ID and comparison code", good: false);
            return;
        }

        var action = new OwnerPairingApprovalAction(pairingId, pairingCode);
        await RunOwnerActionAsync(async () =>
        {
            var approval = await ownerApi.ApprovePairingAsync(
                ResolveWorldUri(), authority, deviceId, action, signer, CancellationToken.None);
            pairingApprovalCode.Text = string.Empty;
            return $"approved pending device {approval.DeviceId}; it must still activate its own Windows key";
        });
    }

    private async Task RevokeDeviceAsync()
    {
        if (!TryGetOwner(out var authority, out var deviceId, out var signer))
        {
            return;
        }

        var targetDeviceId = revokeDeviceId.Text.Trim();
        if (string.IsNullOrWhiteSpace(targetDeviceId))
        {
            SetStatus("enter a paired device ID to revoke it", good: false);
            return;
        }

        if (string.Equals(targetDeviceId, deviceId, StringComparison.Ordinal))
        {
            SetStatus("this client will not revoke its own active key; use host-local recovery if that is intentional", good: false);
            return;
        }

        var action = new OwnerDeviceManagementAction(targetDeviceId);
        await RunOwnerActionAsync(async () =>
        {
            var revoked = await ownerApi.RevokeDeviceAsync(
                ResolveWorldUri(), authority, deviceId, action, signer, CancellationToken.None);
            revokeDeviceId.Text = string.Empty;
            return $"revoked device {revoked.DeviceId}";
        });
        await RefreshDeviceRegistryAsync();
    }

    private async Task RefreshDeviceRegistryAsync()
    {
        if (!TryGetOwner(out var authority, out var deviceId, out var signer))
        {
            return;
        }

        await RunOwnerActionAsync(async () =>
        {
            pairedDevices = await ownerApi.ListDevicesAsync(
                ResolveWorldUri(), authority, deviceId, signer, CancellationToken.None);
            RenderPairedDevices();
            return $"loaded {pairedDevices.Length} paired device record(s)";
        });
    }

    private void RenderPairedDevices()
    {
        pairedDeviceList.Clear();
        foreach (var device in pairedDevices
            .OrderBy(device => device.State == OwnerDeviceState.Active ? 0 : 1)
            .ThenBy(device => device.DeviceId, StringComparer.Ordinal))
        {
            var self = string.Equals(device.DeviceId, registration?.DeviceId, StringComparison.Ordinal)
                ? " · this Windows device"
                : string.Empty;
            pairedDeviceList.AddItem($"{device.State.ToString().ToLowerInvariant()} · {device.DeviceId}{self}");
            pairedDeviceList.SetItemMetadata(pairedDeviceList.ItemCount - 1, device.DeviceId);
        }
    }

    private async Task SaveLifePaceAsync()
    {
        if (!TryGetOwner(out var authority, out var deviceId, out var signer)) return;
        var rate = lifePaceChoice.GetSelectedId();
        await RunOwnerActionAsync(async () =>
        {
            _ = await ownerApi.SetLifePaceAsync(ResolveWorldUri(), authority, deviceId, rate, signer, CancellationToken.None);
            return "life pace saved; current ages preserved, future aging changed";
        });
    }

    private async Task SaveJevAssistanceAsync(bool enabled)
    {
        if (!TryGetOwner(out var authority, out var deviceId, out var signer)) return;
        await RunOwnerActionAsync(async () =>
        {
            _ = await ownerApi.SetJevAssistanceAsync(ResolveWorldUri(), authority, deviceId, enabled, signer, CancellationToken.None);
            return enabled ? "Jev assistance enabled for this world" : "Jev assistance disabled for this world";
        });
    }

    private async Task RefreshProviderConfigurationAsync()
    {
        cognitionApiKeyInput.Text = string.Empty;
        if (!TryGetOwner(out var authority, out var deviceId, out var signer))
        {
            cognitionConfigurationStatus.Text = "Pair this device before configuring inhabitant cognition.";
            RenderProviderConfiguration();
            return;
        }

        await RunOwnerActionAsync(async () =>
        {
            providerConfiguration = await ownerApi.GetProviderStatusAsync(
                ResolveWorldUri(), authority, deviceId, signer, CancellationToken.None);
            PopulateCognitionTargets();
            PopulateProviderChoices(ActiveProviderForSelectedRole());
            PopulateCredentialChoices();
            RenderProviderConfiguration();
            return "loaded inhabitant cognition settings";
        });
    }

    private async Task RefreshUsageAsync()
    {
        if (!TryGetOwner(out var authority, out var deviceId, out var signer))
        {
            usageMeterStatus.Text = "Pair this device to see paid-call usage.";
            return;
        }
        await RunOwnerActionAsync(async () =>
        {
            usageStatus = await ownerApi.GetUsageStatusAsync(ResolveWorldUri(), authority, deviceId,
                signer, CancellationToken.None);
            RenderUsageStatus();
            return "loaded paid-call usage";
        });
    }

    private async Task ObserveUsagePauseAsync()
    {
        if (!TryGetOwner(out var authority, out var deviceId, out var signer)) return;
        try
        {
            usageStatus = await ownerApi.GetUsageStatusAsync(ResolveWorldUri(), authority, deviceId,
                signer, CancellationToken.None);
            RenderUsageStatus();
            if (usageStatus.LimitReached)
                SetStatus("Paid-call limit reached. The world is paused; open World Settings to allow more calls.", good: false);
        }
        catch (Exception)
        {
            // A pause is authoritative even when the optional meter read is unavailable.
        }
    }

    private void RenderUsageStatus()
    {
        if (usageStatus is null)
        {
            usageMeterStatus.Text = "Loading paid-call usage…";
            return;
        }
        if (!usageAttemptLimitInput.HasFocus())
            usageAttemptLimitInput.Text = usageStatus.AttemptLimit?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        var rows = usageStatus.Rows.OrderByDescending(row => row.Attempts)
            .Select(row => $"{row.Provider} / {row.Model} ({row.Role}): {row.Attempts} attempts, " +
                $"{row.InputTokens}/{row.OutputTokens} known tokens in/out");
        usageMeterStatus.Text = $"Installation lifetime · {usageStatus.Attempts} paid call attempts " +
            $"({usageStatus.Completed} completed, {usageStatus.Failed} failed, {usageStatus.Abandoned} abandoned). " +
            $"Known tokens in/out: {usageStatus.InputTokens}/{usageStatus.OutputTokens}. " +
            (usageStatus.AttemptLimit is null ? "Limit off." :
                $"Limit: {usageStatus.AttemptLimit} attempts." +
                (usageStatus.LimitReached ? " LIMIT REACHED — world paused. Consent is needed before more paid calls." : "")) +
            (usageStatus.Rows.Count == 0 ? string.Empty : "\n" + string.Join("\n", rows));
        grantUsageCallsButton.Visible = usageStatus.LimitReached;
        RefreshControlAvailability();
    }

    private async Task ConfigureUsageAsync(bool grant)
    {
        if (!TryGetOwner(out var authority, out var deviceId, out var signer)) return;
        long? cap = null;
        if (!grant && !string.IsNullOrWhiteSpace(usageAttemptLimitInput.Text))
        {
            if (!long.TryParse(usageAttemptLimitInput.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ||
                parsed is < 1 or > 1_000_000)
            {
                SetStatus("Enter 1–1,000,000 paid call attempts, or leave blank to turn the limit off.", good: false);
                return;
            }
            cap = parsed;
        }
        var action = grant ? new OwnerUsageLimitAction(null, AdditionalCalls: 100) :
            new OwnerUsageLimitAction(cap);
        await RunOwnerActionAsync(async () =>
        {
            usageStatus = await ownerApi.ConfigureUsageLimitAsync(ResolveWorldUri(), authority, deviceId,
                action, signer, CancellationToken.None);
            RenderUsageStatus();
            if (grant)
                return "Allowed 100 more paid calls. Resume the world when ready";
            return cap is null ? "paid-call limit turned off" : $"paid-call limit set to {cap} attempts";
        });
    }

    private async Task SaveProviderConfigurationAsync()
    {
        if (!TryGetOwner(out var authority, out var deviceId, out var signer))
        {
            SetStatus("pair this device before configuring cognition", good: false);
            return;
        }

        var role = SelectedRoleId();
        var provider = SelectedProviderId();
        var target = SelectedCognitionTarget();
        var hostedAgent = target is not null && provider is ("openai" or "ollama-cloud");
        var credentialChoice = hostedAgent ? SelectedCredentialChoice() : null;
        var creatingSlot = credentialChoice == "new";
        if (creatingSlot && (string.IsNullOrWhiteSpace(cognitionCredentialLabelInput.Text) ||
            string.IsNullOrWhiteSpace(cognitionApiKeyInput.Text)))
        {
            SetStatus("Give the new key a label and paste its API key", good: false);
            return;
        }
        var action = new OwnerProviderConfigurationAction(
            role,
            provider,
            provider is "deterministic" or "inherit" ? null : EmptyToNull(cognitionModelInput.Text),
            provider is "deterministic" or "inherit" || hostedAgent && !creatingSlot
                ? null : EmptyToNull(cognitionApiKeyInput.Text),
            ForgetCredential: false,
            InhabitantId: target,
            CredentialSlotId: creatingSlot ? Guid.NewGuid().ToString("N") : credentialChoice is null or "default" ? null : credentialChoice,
            NewCredentialLabel: creatingSlot ? EmptyToNull(cognitionCredentialLabelInput.Text) : null);
        try
        {
            await RunOwnerActionAsync(async () =>
            {
                providerConfiguration = await ownerApi.ConfigureProviderAsync(
                    ResolveWorldUri(), authority, deviceId, action, signer, CancellationToken.None);
                PopulateCredentialChoices();
                return $"{ProviderDisplayName(provider)} will handle {RoleDisplayName(role).ToLowerInvariant()} at the next cognition boundary";
            });
        }
        finally
        {
            cognitionApiKeyInput.Text = string.Empty;
            cognitionCredentialLabelInput.Text = string.Empty;
            RenderProviderConfiguration();
        }
    }

    private async Task ForgetProviderCredentialAsync()
    {
        if (!TryGetOwner(out var authority, out var deviceId, out var signer))
        {
            SetStatus("pair this device before changing cognition credentials", good: false);
            return;
        }

        var role = SelectedRoleId();
        var provider = SelectedProviderId();
        if (provider == "deterministic")
        {
            SetStatus("deterministic cognition has no API key", good: false);
            return;
        }

        var action = new OwnerProviderConfigurationAction(
            role,
            provider,
            EmptyToNull(cognitionModelInput.Text),
            null,
            ForgetCredential: true);
        await RunOwnerActionAsync(async () =>
        {
            providerConfiguration = await ownerApi.ConfigureProviderAsync(
                ResolveWorldUri(), authority, deviceId, action, signer, CancellationToken.None);
            return $"forgot the saved {ProviderDisplayName(provider)} key";
        });
        cognitionApiKeyInput.Text = string.Empty;
        RenderProviderConfiguration();
    }

    private async Task DeleteCredentialSlotAsync()
    {
        if (!TryGetOwner(out var authority, out var deviceId, out var signer))
        {
            SetStatus("pair this device before deleting a saved key", good: false);
            return;
        }

        var slotId = SelectedCredentialChoice();
        var slot = providerConfiguration?.CredentialSlots?.FirstOrDefault(item => item.Id == slotId);
        if (slot is null)
        {
            SetStatus("select a named key to delete", good: false);
            return;
        }
        if (providerConfiguration?.Assignments?.Any(item => item.CredentialSlotId == slotId) == true)
        {
            SetStatus("this key is assigned to an agent; choose another key for that agent first", good: false);
            return;
        }

        await RunOwnerActionAsync(async () =>
        {
            providerConfiguration = await ownerApi.DeleteCredentialSlotAsync(
                ResolveWorldUri(), authority, deviceId, slotId, signer, CancellationToken.None);
            PopulateCredentialChoices();
            RenderProviderConfiguration();
            return $"deleted saved key {slot.Label}";
        });
    }

    private string SelectedRoleId() => SelectedCognitionTarget() is not null
        ? "personal" : cognitionRoleChoice.Selected == 1 ? "planning" : "routine";

    private string? SelectedCognitionTarget() => cognitionTargetChoice.Selected <= 0
        ? null : cognitionTargetChoice.GetItemMetadata(cognitionTargetChoice.Selected).AsString();

    private bool SelectedTargetWasBornHere() => observationSession.Current?.Baseline.Snapshot.Inhabitants
        .FirstOrDefault(item => item.Id == SelectedCognitionTarget())?.Relationships
        .Any(item => item.Type == "biological_parentage" && item.Direction == "child") == true;

    private void PopulateCognitionTargets()
    {
        var target = SelectedCognitionTarget();
        cognitionTargetChoice.Clear();
        cognitionTargetChoice.AddItem("World defaults");
        foreach (var inhabitant in observationSession.Current?.Baseline.Snapshot.Inhabitants ?? [])
        {
            cognitionTargetChoice.AddItem(inhabitant.DisplayName);
            var index = cognitionTargetChoice.ItemCount - 1;
            cognitionTargetChoice.SetItemMetadata(index, inhabitant.Id);
            if (inhabitant.Id == target)
            {
                cognitionTargetChoice.Select(index);
            }
        }
    }

    private InhabitantProviderAssignment? SelectedAssignment() => providerConfiguration?.Assignments?
        .FirstOrDefault(item => item.InhabitantId == SelectedCognitionTarget() &&
            item.Role == (SelectedRoleId() == "personal" ? "planning" : SelectedRoleId()));

    private string ActiveProviderForSelectedRole() => SelectedCognitionTarget() is not null
        ? SelectedAssignment()?.Provider ?? "inherit"
        : providerConfiguration is null
        ? "deterministic"
        : SelectedRoleId() == "planning"
            ? providerConfiguration.PlanningProvider
            : providerConfiguration.RoutineProvider;

    private void PopulateProviderChoices(string selectedProvider)
    {
        cognitionProviderChoice.Clear();
        if (SelectedCognitionTarget() is not null)
        {
            AddProviderChoice(SelectedTargetWasBornHere()
                ? "No personal model (safe local)" : "Use world default", "inherit");
        }
        AddProviderChoice("Deterministic", "deterministic");
        if (SelectedRoleId() == "routine" && SelectedCognitionTarget() is null)
        {
            AddProviderChoice("Jev", "jev");
        }
        if (SelectedRoleId() == "planning" || SelectedCognitionTarget() is not null)
        {
            AddProviderChoice("OpenAI", "openai");
            AddProviderChoice("Ollama Cloud", "ollama-cloud");
        }
        if (SelectedCognitionTarget() is not null && selectedProvider == "jev")
            AddProviderChoice("Jev (legacy assignment)", "jev");

        SelectProviderChoice(selectedProvider);
    }

    private void SelectProviderChoice(string provider)
    {
        for (var index = 0; index < cognitionProviderChoice.ItemCount; index++)
        {
            if (cognitionProviderChoice.GetItemMetadata(index).AsString() == provider)
            {
                cognitionProviderChoice.Select(index);
                return;
            }
        }
        cognitionProviderChoice.Select(0);
    }

    private void AddProviderChoice(string label, string id)
    {
        cognitionProviderChoice.AddItem(label);
        cognitionProviderChoice.SetItemMetadata(cognitionProviderChoice.ItemCount - 1, id);
    }

    private string SelectedProviderId() => cognitionProviderChoice.Selected < 0
        ? "deterministic" : cognitionProviderChoice.GetItemMetadata(cognitionProviderChoice.Selected).AsString();

    private string SelectedCredentialChoice() => cognitionCredentialChoice.Selected < 0
        ? "default" : cognitionCredentialChoice.GetItemMetadata(cognitionCredentialChoice.Selected).AsString();

    private void PopulateCredentialChoices()
    {
        cognitionCredentialChoice.Clear();
        cognitionCredentialChoice.AddItem("Provider default key");
        cognitionCredentialChoice.SetItemMetadata(0, "default");
        var provider = SelectedProviderId();
        foreach (var slot in providerConfiguration?.CredentialSlots ?? [])
        {
            if (slot.Provider != provider) continue;
            cognitionCredentialChoice.AddItem(slot.Label);
            cognitionCredentialChoice.SetItemMetadata(cognitionCredentialChoice.ItemCount - 1, slot.Id);
        }
        cognitionCredentialChoice.AddItem("Add another API key…");
        cognitionCredentialChoice.SetItemMetadata(cognitionCredentialChoice.ItemCount - 1, "new");
        var assignedSlot = SelectedAssignment()?.Provider == provider ? SelectedAssignment()?.CredentialSlotId : null;
        for (var index = 0; index < cognitionCredentialChoice.ItemCount; index++)
        {
            if (cognitionCredentialChoice.GetItemMetadata(index).AsString() != assignedSlot) continue;
            cognitionCredentialChoice.Select(index);
            return;
        }
        cognitionCredentialChoice.Select(0);
    }

    private void RenderProviderConfiguration()
    {
        var provider = SelectedProviderId();
        var option = providerConfiguration?.Providers.FirstOrDefault(item =>
            string.Equals(item.Provider, provider, StringComparison.Ordinal));
        var hosted = provider is not ("deterministic" or "inherit");
        cognitionRoleChoice.Visible = SelectedCognitionTarget() is null;
        var agentCredential = hosted && SelectedCognitionTarget() is not null && provider is ("openai" or "ollama-cloud");
        var newCredential = agentCredential && SelectedCredentialChoice() == "new";
        cognitionModelInput.Visible = hosted;
        cognitionCredentialChoice.Visible = agentCredential;
        cognitionCredentialLabelInput.Visible = newCredential;
        cognitionApiKeyInput.Visible = hosted && (!agentCredential || newCredential);
        cognitionCredentialHint.Visible = hosted;
        forgetCognitionCredentialButton.Visible = hosted && SelectedCognitionTarget() is null;
        deleteCognitionCredentialSlotButton.Visible = agentCredential &&
            SelectedCredentialChoice() is not ("default" or "new");
        if (hosted && option is not null && !cognitionModelInput.HasFocus())
        {
            cognitionModelInput.Text = SelectedAssignment() is { } assignment && assignment.Provider == provider
                ? assignment.Model ?? option.Model : option.Model;
        }

        cognitionApiKeyInput.PlaceholderText = newCredential ? "New API key" : option?.HasCredential == true
            ? "Leave blank to keep saved key"
            : "API key";
        cognitionCredentialHint.Text = newCredential
            ? "A new key is stored privately on the host and can be reused for other agents."
            : agentCredential && SelectedCredentialChoice() != "default"
            ? "Named key saved on host"
            : agentCredential && option?.HasCredential != true
            ? "No provider default key. Select Add another API key to give this agent one."
            : option?.HasCredential == true
            ? "Key saved on host"
            : "No saved key";
        cognitionConfigurationStatus.Text = providerConfiguration is null
            ? "Loading…"
            : SelectedTargetWasBornHere() && SelectedAssignment() is null
            ? "No personal model selected for this child. After infancy, safe local decisions continue until a model is assigned; world defaults are not used."
            : $"Routine: {ProviderDisplayName(providerConfiguration.RoutineProvider)} · Planning: {ProviderDisplayName(providerConfiguration.PlanningProvider)}";
        RefreshControlAvailability();
    }

    private static string RoleDisplayName(string role) => role == "planning"
        ? "Planning and work decisions"
        : role == "personal" ? "this agent's decisions"
        : "Routine survival decisions";

    private static string ProviderDisplayName(string provider) => provider switch
    {
        "jev" => "Jev",
        "openai" => "OpenAI",
        "ollama-cloud" => "Ollama Cloud",
        "inherit" => "World default",
        _ => "Deterministic",
    };

    private static string DefaultProviderModel(string provider) => provider switch
    {
        "jev" => "jev-1.13.0",
        "openai" => "gpt-5-mini",
        "ollama-cloud" => "gpt-oss:120b-cloud",
        _ => string.Empty,
    };

    private bool TryBeginPendingInstruction(
        OwnerInstructionAction action,
        out OwnerPendingSubmission pending)
    {
        pending = null!;
        if (!TryCreatePendingSubmissionBinding(out var binding))
        {
            SetStatus("cannot retain an instruction until this paired device has a valid pinned server origin", good: false);
            return false;
        }

        pending = OwnerPendingSubmission.ForInstruction(binding, action);
        return TryRetainPendingSubmission(pending);
    }

    private bool TryBeginPendingAuthoring(
        OwnerAuthoringBatchAction action,
        out OwnerPendingSubmission pending)
    {
        pending = null!;
        if (!TryCreatePendingSubmissionBinding(out var binding))
        {
            SetStatus("cannot retain authoring until this paired device has a valid pinned server origin", good: false);
            return false;
        }

        pending = OwnerPendingSubmission.ForAuthoring(binding, action);
        return TryRetainPendingSubmission(pending);
    }

    private bool TryRetainPendingSubmission(OwnerPendingSubmission candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (pendingSubmission is not null)
        {
            SetStatus("a prior owner request is awaiting confirmation; retry it or explicitly forget it first", good: false);
            return false;
        }

        if (!pendingSubmissionStore.TrySave(candidate))
        {
            SetStatus("could not retain the owner request locally; retry or forget the existing local retry record first", good: false);
            return false;
        }

        pendingSubmission = candidate;
        RenderPendingSubmission();
        RefreshControlAvailability();
        return true;
    }

    private void LoadPendingSubmission()
    {
        pendingSubmission = TryCreatePendingSubmissionBinding(out var binding)
            ? pendingSubmissionStore.TryLoad(binding)
            : null;
        RenderPendingSubmission();
        RefreshControlAvailability();
    }

    private bool TryCreatePendingSubmissionBinding(out OwnerPendingSubmissionBinding binding)
    {
        binding = null!;
        if (registeredEndpointInvalid || registration is null || deviceKey is null)
        {
            return false;
        }

        try
        {
            binding = OwnerPendingSubmissionBinding.Create(
                registration.Authority,
                registration.DeviceId,
                deviceKey.PublicKeyFingerprint,
                ResolveWorldUri());
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private async Task RetryPendingSubmissionAsync()
    {
        var pending = pendingSubmission;
        if (pending is null)
        {
            SetStatus("there is no loaded owner request to retry", good: false);
            return;
        }

        if (!TryGetOwner(out var authority, out var deviceId, out var signer) ||
            !TryCreatePendingSubmissionBinding(out var binding) ||
            !pending.Binding.Matches(binding))
        {
            SetStatus("this retained request is not bound to the current paired device and pinned server; forget it explicitly before making a new request", good: false);
            return;
        }

        var completed = false;
        await RunOwnerActionAsync(async () =>
        {
            if (pending.Instruction is { } instruction)
            {
                var receipt = await ownerApi.SubmitInstructionAsync(
                    ResolveWorldUri(), authority, deviceId, instruction.ToAction(), signer, CancellationToken.None);
                completed = true;
                return $"confirmed {instruction.Kind} instruction {receipt.InstructionId}";
            }

            if (pending.Authoring is { } authoring)
            {
                var receipt = await ownerApi.SubmitAuthoringAsync(
                    ResolveWorldUri(), authority, deviceId, authoring.ToAction(), signer, CancellationToken.None);
                completed = true;
                return receipt.Applied
                    ? $"confirmed authoring batch {receipt.BatchId} at revision {receipt.Revision}"
                    : $"authoring batch rejected · {receipt.Failure ?? "unknown validation failure"}";
            }

            throw new InvalidOperationException("The retained owner request has no supported payload.");
        });

        if (completed)
        {
            CompletePendingSubmission(pending);
        }
    }

    private void CompletePendingSubmission(OwnerPendingSubmission completed)
    {
        if (!pendingSubmissionStore.TryClear(completed))
        {
            SetStatus("server confirmed the request, but its local retry record could not be cleared; retry remains safe or forget it after checking the world", good: false);
            return;
        }

        if (ReferenceEquals(pendingSubmission, completed))
        {
            pendingSubmission = null;
        }

        RenderPendingSubmission();
        RefreshControlAvailability();
    }

    private void ForgetPendingSubmission()
    {
        if (!pendingSubmissionStore.TryForget())
        {
            SetStatus("could not discard the local retry record", good: false);
            return;
        }

        pendingSubmission = null;
        RenderPendingSubmission();
        RefreshControlAvailability();
        SetStatus("discarded the local retry record; no server state was changed", good: false);
    }

    private void RenderPendingSubmission()
    {
        pendingSubmissionLabel.Text = pendingSubmission switch
        {
            { Instruction: { } instruction } =>
                $"Retained instruction retry · {instruction.Kind} for {instruction.TargetInhabitantId} · ID {instruction.IdempotencyKey}",
            { Authoring: { } authoring } =>
                $"Retained paused-authoring retry · batch {authoring.BatchId}",
            _ => "No retained owner request. A network failure keeps one instruction or authoring batch here for an exact retry.",
        };
    }

    private async Task RunOwnerActionAsync(Func<Task<string>> action)
    {
        if (isOwnerAction)
        {
            return;
        }

        isOwnerAction = true;
        RefreshControlAvailability();
        try
        {
            SetStatus("submitting one-use signed owner request…", good: true);
            var detail = await action();
            SetStatus(detail, good: true);
            await RefreshAsync();
        }
        catch (Exception exception)
        {
            ShowHeldState($"owner request rejected or unavailable · {FriendlyFailure(exception)}");
        }
        finally
        {
            isOwnerAction = false;
            RefreshControlAvailability();
        }
    }

    private async Task RenameSelectedAgentAsync()
    {
        if (selectedInhabitantId is not { } agentId ||
            observationSession.Current?.Baseline.Snapshot.Inhabitants.All(person => person.Id != agentId) != false)
            return;
        var name = renameAgentInput.Text.Trim();
        if (name.Length is < 1 or > 48 || name.Any(char.IsControl))
        {
            SetStatus("Choose a name of at most 48 characters", good: false);
            return;
        }
        if (!TryGetOwner(out var authority, out var deviceId, out var signer)) return;
        await RunOwnerActionAsync(async () =>
        {
            var result = await ownerApi.RenameAgentAsync(ResolveWorldUri(), authority, deviceId,
                new OwnerAgentRenameAction(agentId, name), signer, CancellationToken.None);
            renamingAgentId = null;
            return result.Changed ? $"Renamed to {result.Name}" : "Name unchanged";
        });
    }

    private bool TryGetOwner(
        out OwnerAuthorityIdentity authority,
        out string deviceId,
        out IOwnerDeviceSigner signer)
    {
        if (!registeredEndpointInvalid && registration is not null && deviceKey is not null)
        {
            authority = registration.Authority;
            deviceId = registration.DeviceId;
            signer = deviceKey;
            return true;
        }

        authority = null!;
        deviceId = string.Empty;
        signer = null!;
        return false;
    }

    private void BuildLayout()
    {
        AddThemeColorOverride("font_color", new Color("E5EFEA"));
        AddThemeFontSizeOverride("font_size", 14);

        var backdrop = new ColorRect
        {
            Color = new Color("0D151C"),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(backdrop);

        var root = new VBoxContainer();
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 0);
        AddChild(root);

        BuildTopBar(root);
        BuildWorldColumn(root);
        BuildFounderSetupPanel(mapCanvas);
        BuildInspectorColumn(mapCanvas);
        BuildOwnerColumn(mapCanvas);
        BuildStatusToast(mapCanvas);
        BuildMainMenu();
        BuildManualSavesPanel();

        Resized += ApplyResponsiveLayout;
        ApplyResponsiveLayout();
    }

    private void BuildTopBar(Control content)
    {
        var chrome = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, 60),
        };
        chrome.AddThemeStyleboxOverride("panel", TopBarStyle());
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_top", 9);
        margin.AddThemeConstantOverride("margin_bottom", 9);

        topBar.AddThemeConstantOverride("separation", 8);
        mapButton.Text = "Map";
        mapButton.TooltipText = "Open the world overview; zoom with the mouse wheel and pan with WASD or middle-drag.";
        StyleButton(mapButton);
        mapButton.Pressed += () =>
        {
            var show = !worldOverviewPanel.Visible;
            rosterPanel.Hide();
            settlementPanel.Hide();
            eventsPanel.Hide();
            familyTreePanel.Hide();
            worldInfoPanel.Hide();
            worldOverviewPanel.Visible = show;
        };
        topBar.AddChild(mapButton);

        clockLabel.Text = "Connecting…";
        clockLabel.AddThemeFontSizeOverride("font_size", 20);
        clockLabel.AddThemeColorOverride("font_color", new Color("F4F0E3"));
        topBar.AddChild(clockLabel);

        climateLabel.Text = string.Empty;
        climateLabel.Modulate = new Color("AFC4BA");
        climateLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        topBar.AddChild(climateLabel);

        worldInfoButton.Text = "Info";
        worldInfoButton.TooltipText = "World Info";
        StyleButton(worldInfoButton);
        worldInfoButton.Pressed += ToggleWorldInfo;
        topBar.AddChild(worldInfoButton);

        inhabitantsButton.Text = "Inhabitants";
        StyleButton(inhabitantsButton);
        inhabitantsButton.Pressed += ToggleInhabitants;
        topBar.AddChild(inhabitantsButton);

        settlementButton.Text = "Settlement";
        StyleButton(settlementButton);
        settlementButton.Pressed += () =>
        {
            rosterPanel.Hide();
            eventsPanel.Hide();
            worldOverviewPanel.Hide();
            worldInfoPanel.Hide();
            settlementPanel.Visible = !settlementPanel.Visible;
        };
        topBar.AddChild(settlementButton);

        eventsButton.Text = "Events";
        StyleButton(eventsButton);
        eventsButton.Pressed += ToggleEvents;
        topBar.AddChild(eventsButton);

        founderSetupButton.Text = "Add founders";
        StyleButton(founderSetupButton);
        founderSetupButton.Pressed += () => _ = ToggleFounderSetupAsync();
        topBar.AddChild(founderSetupButton);

        startWorldButton.Text = "Start World";
        StyleButton(startWorldButton, primary: true);
        startWorldButton.Pressed += () => _ = StartFounderWorldAsync();
        topBar.AddChild(startWorldButton);

        pauseButton.Text = "Pause";
        StyleButton(pauseButton, primary: true);
        pauseButton.Pressed += () => _ = TogglePauseAsync();
        topBar.AddChild(pauseButton);

        addAgentButton.Text = "Add Agent";
        StyleButton(addAgentButton);
        addAgentButton.Pressed += () => _ = ToggleAddAgentAsync();
        topBar.AddChild(addAgentButton);

        menuButton.Text = "Menu";
        StyleButton(menuButton);
        menuButton.Pressed += () => _ = ToggleGameMenuAsync();
        topBar.AddChild(menuButton);

        margin.AddChild(topBar);
        chrome.AddChild(margin);
        content.AddChild(chrome);
    }

    private void BuildConnectionPanel()
    {
        var body = new HBoxContainer();
        body.AddThemeConstantOverride("separation", 8);
        worldUrlInput.PlaceholderText = "https://your-tailnet-host:8443";
        worldUrlInput.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        body.AddChild(worldUrlInput);
        connectButton.Text = "Connect";
        connectButton.Pressed += () => _ = ConnectUsingCurrentUrlAsync();
        body.AddChild(connectButton);
        pairAgainButton.Text = "Pair again";
        pairAgainButton.TooltipText = "Replace this device's saved world registration and pair its Windows key with the current world.";
        pairAgainButton.Visible = false;
        pairAgainButton.Pressed += () => _ = PairAgainAsync();
        body.AddChild(pairAgainButton);
        AddPanelContents(connectionPanel, "World connection", body);
    }

    private void BuildCognitionSettingsPanel()
    {
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 6);

        cognitionTargetChoice.AddItem("World defaults");
        cognitionTargetChoice.ItemSelected += _ =>
        {
            cognitionApiKeyInput.Text = string.Empty;
            PopulateProviderChoices(ActiveProviderForSelectedRole());
            PopulateCredentialChoices();
            RenderProviderConfiguration();
        };
        body.AddChild(cognitionTargetChoice);

        cognitionRoleChoice.AddItem("Routine survival");
        cognitionRoleChoice.AddItem("Planning and work");
        cognitionRoleChoice.TooltipText = "Routine handles daily needs; planning chooses projects. Agent models may handle either role. Jev assistance is switched on or off for the whole world.";
        cognitionRoleChoice.ItemSelected += _ =>
        {
            cognitionApiKeyInput.Text = string.Empty;
            PopulateProviderChoices(ActiveProviderForSelectedRole());
            var selected = SelectedProviderId();
            var option = providerConfiguration?.Providers.FirstOrDefault(item => item.Provider == selected);
            cognitionModelInput.Text = option?.Model ?? DefaultProviderModel(selected);
            PopulateCredentialChoices();
            RenderProviderConfiguration();
        };
        var providerRow = new HBoxContainer();
        cognitionRoleChoice.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        cognitionProviderChoice.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        providerRow.AddChild(cognitionRoleChoice);

        PopulateProviderChoices("deterministic");
        cognitionProviderChoice.ItemSelected += _ =>
        {
            cognitionApiKeyInput.Text = string.Empty;
            var selected = SelectedProviderId();
            var option = providerConfiguration?.Providers.FirstOrDefault(item => item.Provider == selected);
            cognitionModelInput.Text = option?.Model ?? DefaultProviderModel(selected);
            PopulateCredentialChoices();
            RenderProviderConfiguration();
        };
        providerRow.AddChild(cognitionProviderChoice);
        body.AddChild(providerRow);

        cognitionCredentialChoice.TooltipText = "Pick a saved key for this agent, or save another key for the same provider.";
        cognitionCredentialChoice.ItemSelected += _ =>
        {
            cognitionApiKeyInput.Text = string.Empty;
            RenderProviderConfiguration();
        };
        body.AddChild(cognitionCredentialChoice);

        cognitionCredentialLabelInput.PlaceholderText = "Name this key (for example, Personal account)";
        body.AddChild(cognitionCredentialLabelInput);

        cognitionModelInput.PlaceholderText = "Model ID";
        body.AddChild(cognitionModelInput);

        cognitionApiKeyInput.Secret = true;
        cognitionApiKeyInput.PlaceholderText = "Paste API key";
        body.AddChild(cognitionApiKeyInput);

        cognitionCredentialHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        cognitionCredentialHint.Modulate = new Color("8FA5A7");
        body.AddChild(cognitionCredentialHint);

        cognitionCredentialHint.TooltipText = "Keys travel over paired HTTPS and stay on the host. They are never returned, logged, or included in world saves.";

        cognitionConfigurationStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        body.AddChild(cognitionConfigurationStatus);

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 6);
        saveCognitionProviderButton.Text = "Apply";
        StyleButton(saveCognitionProviderButton, primary: true);
        saveCognitionProviderButton.Pressed += () => _ = SaveProviderConfigurationAsync();
        buttons.AddChild(saveCognitionProviderButton);
        forgetCognitionCredentialButton.Text = "Remove key";
        StyleButton(forgetCognitionCredentialButton);
        forgetCognitionCredentialButton.Pressed += () => _ = ForgetProviderCredentialAsync();
        buttons.AddChild(forgetCognitionCredentialButton);
        deleteCognitionCredentialSlotButton.Text = "Delete named key";
        deleteCognitionCredentialSlotButton.TooltipText = "Delete an unused named API key from this installation. First switch any agents assigned to it.";
        StyleButton(deleteCognitionCredentialSlotButton);
        deleteCognitionCredentialSlotButton.Pressed += () => _ = DeleteCredentialSlotAsync();
        buttons.AddChild(deleteCognitionCredentialSlotButton);
        refreshCognitionProviderButton.Text = "Refresh";
        StyleButton(refreshCognitionProviderButton);
        refreshCognitionProviderButton.Pressed += () => _ = RefreshProviderConfigurationAsync();
        buttons.AddChild(refreshCognitionProviderButton);
        body.AddChild(buttons);

        body.AddChild(new Label { Text = "Paid model usage · installation lifetime" });
        usageMeterStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        body.AddChild(usageMeterStatus);
        usageAttemptLimitInput.PlaceholderText = "Optional paid-call attempt limit (blank = off)";
        usageAttemptLimitInput.TooltipText = "Counts every hosted call attempt, including retries and abandoned calls. Tokens are informational, not the limit unit.";
        body.AddChild(usageAttemptLimitInput);
        var usageButtons = new HBoxContainer();
        applyUsageLimitButton.Text = "Apply limit";
        StyleButton(applyUsageLimitButton);
        applyUsageLimitButton.Pressed += () => _ = ConfigureUsageAsync(grant: false);
        usageButtons.AddChild(applyUsageLimitButton);
        grantUsageCallsButton.Text = "Allow 100 more calls";
        grantUsageCallsButton.TooltipText = "Explicitly consent to 100 more paid model attempts. Resume the paused world separately when ready.";
        StyleButton(grantUsageCallsButton, primary: true);
        grantUsageCallsButton.Pressed += () => _ = ConfigureUsageAsync(grant: true);
        grantUsageCallsButton.Visible = false;
        usageButtons.AddChild(grantUsageCallsButton);
        refreshUsageButton.Text = "Refresh usage";
        StyleButton(refreshUsageButton);
        refreshUsageButton.Pressed += () => _ = RefreshUsageAsync();
        usageButtons.AddChild(refreshUsageButton);
        body.AddChild(usageButtons);

        AddPanelContents(cognitionSettingsPanel, "Inhabitant cognition", body);
        RenderProviderConfiguration();
        RenderUsageStatus();
    }

    private void ShowSettingsSection(bool worldSpecific)
    {
        if (worldSpecific && (!isInWorld || returnToMainMenu)) return;
        CloseAgentModelEditor();
        modLibraryPanel.Hide();
        settingsPanel.Show();
        gameSettingsContent.Visible = !worldSpecific;
        worldSettingsContent.Visible = worldSpecific;
        gameSettingsCategoryButton.Disabled = !worldSpecific;
        worldSettingsCategoryButton.Disabled = worldSpecific || registration is null;
        settingsScroll.Show();
        developerScroll.Hide();
        developerToggleButton.Disabled = false;
        developerToggleButton.Text = "Developer tools";
        if (worldSpecific && registration is not null)
        {
            _ = RefreshWorldSettingsAsync();
        }

        ApplyResponsiveLayout();
    }

    private async Task RefreshWorldSettingsAsync()
    {
        await RefreshProviderConfigurationAsync();
        await RefreshUsageAsync();
        await RefreshAutosaveSettingsAsync();
    }

    private async Task ConnectUsingCurrentUrlAsync()
    {
        if (registeredEndpointInvalid)
        {
            SetStatus("saved paired endpoint is invalid · forget this local registration before pairing again", good: false);
            return;
        }

        try
        {
            _ = ResolveWorldUri();
        }
        catch (Exception exception)
        {
            SetStatus($"world URL is invalid · {FriendlyFailure(exception)}", good: false);
            return;
        }

        if (registration is not null)
        {
            await RefreshAsync();
            return;
        }

        await StartPairingAsync();
    }

    private async Task PairAgainAsync()
    {
        if (isPairingOperation || isOwnerAction || isRefreshing)
        {
            return;
        }

        ForgetLocalRegistration();
        await StartPairingAsync();
    }

    private void BuildPairingPanel()
    {
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 6);
        var heading = new Label { Text = "PAIR THIS WINDOWS DEVICE" };
        heading.AddThemeFontSizeOverride("font_size", 16);
        body.AddChild(heading);
        pairingInstructionLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        body.AddChild(pairingInstructionLabel);
        var codeRow = new HBoxContainer();
        codeRow.AddChild(new Label { Text = "comparison code:" });
        pairingCodeLabel.AddThemeFontSizeOverride("font_size", 22);
        codeRow.AddChild(pairingCodeLabel);
        body.AddChild(codeRow);
        var idRow = new HBoxContainer();
        idRow.AddChild(new Label { Text = "pairing ID:" });
        pairingIdLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        idRow.AddChild(pairingIdLabel);
        body.AddChild(idRow);
        body.AddChild(pairingExpiryLabel);
        var buttons = new HBoxContainer();
        pairButton.Text = "Start pairing";
        pairButton.Pressed += () => _ = StartPairingAsync();
        buttons.AddChild(pairButton);
        forgetRegistrationButton.Text = "Forget local registration";
        forgetRegistrationButton.Pressed += ForgetLocalRegistration;
        buttons.AddChild(forgetRegistrationButton);
        body.AddChild(buttons);
        AddPanelContents(pairingPanel, body);
        pairingPanel.Hide();
    }

    private void BuildWorldColumn(Control content)
    {
        mapCanvas.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        mapCanvas.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        mapCanvas.ClipContents = true;

        var worldBackdrop = new ColorRect
        {
            Color = new Color("101A1E"),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        worldBackdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        mapCanvas.AddChild(worldBackdrop);

        mapStage.MouseFilter = Control.MouseFilterEnum.Ignore;
        mapCanvas.AddChild(mapStage);

        mapStage.AddChild(terrainLayer);

        objectLayer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        objectLayer.MouseFilter = Control.MouseFilterEnum.Ignore;
        mapStage.AddChild(objectLayer);

        entityLayer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        entityLayer.MouseFilter = Control.MouseFilterEnum.Ignore;
        mapStage.AddChild(entityLayer);

        BuildSelectedInhabitantCard();
        mapCanvas.AddChild(selectedInhabitantCard);

        worldOverview.CenterRequested += CenterCameraAt;
        AddPanelContents(worldOverviewPanel, "World Map", worldOverview);
        worldOverviewPanel.Position = new Vector2(14, 14);
        worldOverviewPanel.ZIndex = 80;
        worldOverviewPanel.Hide();
        mapCanvas.AddChild(worldOverviewPanel);
        mapCanvas.GuiInput += HandleMapInput;
        mapCanvas.MouseExited += () => terrainLayer.SetHoveredTile(null);
        content.AddChild(mapCanvas);
    }

    private void BuildInspectorColumn(Control content)
    {
        var rosterBody = new VBoxContainer();
        rosterBody.AddThemeConstantOverride("separation", 6);
        rosterSummaryLabel.Text = "Waiting for the world…";
        rosterSummaryLabel.Modulate = new Color("A7B9B7");
        rosterSummaryLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        rosterBody.AddChild(rosterSummaryLabel);

        inhabitantList.CustomMinimumSize = new Vector2(300, 260);
        inhabitantList.ItemSelected += index => SelectInhabitantFromList(index);
        inhabitantList.TooltipText = "Choose someone to find them in the world.";
        rosterBody.AddChild(inhabitantList);
        AddPanelContents(rosterPanel, "Inhabitants", rosterBody);
        rosterPanel.CustomMinimumSize = new Vector2(330, 330);
        rosterPanel.ZIndex = 80;
        rosterPanel.Hide();
        content.AddChild(rosterPanel);

        ConfigureTextPanel(eventLog, 300);
        eventLog.MetaClicked += meta => JumpToEvent(meta.AsString());
        eventLog.TooltipText = "Click a located event to jump to where it happened.";
        AddPanelContents(eventsPanel, "Recent events", eventLog);
        eventsPanel.CustomMinimumSize = new Vector2(390, 360);
        eventsPanel.ZIndex = 80;
        eventsPanel.Hide();
        content.AddChild(eventsPanel);

        var familyBody = new VBoxContainer();
        var familyHeading = new HBoxContainer();
        var familyTitle = new Label { Text = "Family Tree", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        familyTitle.AddThemeFontSizeOverride("font_size", 18);
        familyHeading.AddChild(familyTitle);
        var closeFamily = new Button { Text = "×", TooltipText = "Close family tree" };
        StyleButton(closeFamily);
        closeFamily.Pressed += () => familyTreePanel.Hide();
        familyHeading.AddChild(closeFamily);
        familyBody.AddChild(familyHeading);
        familyTreeStatus.Text = "Green: parent–child   ·   Pink: partnership   ·   Click a person to inspect";
        familyBody.AddChild(familyTreeStatus);
        var familyScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        familyScroll.AddChild(familyTreeView);
        familyBody.AddChild(familyScroll);
        familyTreeView.PersonRequested += SelectFromFamilyTree;
        AddPanelContents(familyTreePanel, familyBody);
        familyTreePanel.ZIndex = 85;
        familyTreePanel.Hide();
        content.AddChild(familyTreePanel);

        var memoriesBody = new VBoxContainer();
        var memoriesHeading = new HBoxContainer();
        var memoriesTitle = new Label { Text = "Memories", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        memoriesTitle.AddThemeFontSizeOverride("font_size", 18);
        memoriesHeading.AddChild(memoriesTitle);
        var closeMemories = new Button { Text = "×", TooltipText = "Close memories" };
        StyleButton(closeMemories);
        closeMemories.Pressed += () => memoriesPanel.Hide();
        memoriesHeading.AddChild(closeMemories);
        memoriesBody.AddChild(memoriesHeading);
        ConfigureTextPanel(memoryHistory, 300);
        memoryHistory.TooltipText = "This is the selected agent's saved memory, not the authoritative world event log.";
        memoriesBody.AddChild(memoryHistory);
        AddPanelContents(memoriesPanel, memoriesBody);
        memoriesPanel.ZIndex = 85;
        memoriesPanel.Hide();
        content.AddChild(memoriesPanel);

        ConfigureTextPanel(worldDetails, 320);
        AddPanelContents(settlementPanel, "Settlement · stores and projects", worldDetails);
        settlementPanel.CustomMinimumSize = new Vector2(420, 380);
        settlementPanel.ZIndex = 80;
        settlementPanel.Hide();
        content.AddChild(settlementPanel);

        ConfigureTextPanel(worldInfoText, 220);
        AddPanelContents(worldInfoPanel, "World Info", worldInfoText);
        worldInfoPanel.CustomMinimumSize = new Vector2(365, 280);
        worldInfoPanel.ZIndex = 80;
        worldInfoPanel.Hide();
        content.AddChild(worldInfoPanel);

        var tileBody = new VBoxContainer();
        var tileHeading = new HBoxContainer();
        tileHeading.AddChild(new Label { Text = "Selected tile", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var closeTile = new Button { Text = "×", TooltipText = "Close tile inspection" };
        StyleButton(closeTile);
        closeTile.Pressed += ClearTileSelection;
        tileHeading.AddChild(closeTile);
        tileBody.AddChild(tileHeading);
        ConfigureTextPanel(selectedTileText, 185);
        tileBody.AddChild(selectedTileText);
        AddPanelContents(selectedTilePanel, tileBody);
        selectedTilePanel.CustomMinimumSize = new Vector2(315, 235);
        selectedTilePanel.ZIndex = 80;
        selectedTilePanel.Hide();
        content.AddChild(selectedTilePanel);
    }

    private void BuildOwnerColumn(Control content)
    {
        menuShade.Color = new Color(0, 0, 0, 0.46f);
        menuShade.MouseFilter = Control.MouseFilterEnum.Stop;
        menuShade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        menuShade.ZIndex = 90;
        menuShade.Hide();
        content.AddChild(menuShade);

        var body = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(360, 0),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        body.AddThemeConstantOverride("separation", 8);

        var menuHeading = new HBoxContainer();
        menuHeadingLabel.Text = "Paused";
        menuHeadingLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        menuHeadingLabel.AddThemeFontSizeOverride("font_size", 24);
        menuHeadingLabel.AddThemeColorOverride("font_color", new Color("F4F0E3"));
        menuHeading.AddChild(menuHeadingLabel);
        var closeButton = new Button { Text = "×", TooltipText = "Return to the world" };
        StyleButton(closeButton);
        closeButton.Pressed += () => _ = CloseGameMenuAsync();
        menuHeading.AddChild(closeButton);
        body.AddChild(menuHeading);

        var menuActions = new VBoxContainer();
        menuActions.AddThemeConstantOverride("separation", 6);
        menuResumeButton.Text = "Resume";
        StyleButton(menuResumeButton, primary: true);
        menuResumeButton.Pressed += () => _ = CloseGameMenuAsync();
        menuResumeButton.Hide();
        menuActions.AddChild(menuResumeButton);

        menuSaveWorldButton.Text = "Save World";
        StyleButton(menuSaveWorldButton);
        menuSaveWorldButton.Pressed += () => _ = OpenManualSavesAsync(loadMode: false);
        menuActions.AddChild(menuSaveWorldButton);

        settingsButton.Text = "Settings";
        StyleButton(settingsButton);
        settingsButton.Pressed += () => ShowSettingsSection(worldSpecific: false);
        menuActions.AddChild(settingsButton);

        modLibraryButton.Text = "Mod Library";
        StyleButton(modLibraryButton);
        modLibraryButton.Pressed += ShowModLibrary;
        menuActions.AddChild(modLibraryButton);

        developerToggleButton.Text = "Developer tools";
        StyleButton(developerToggleButton);
        developerToggleButton.Pressed += () =>
        {
            settingsScroll.Hide();
            developerScroll.Show();
            gameSettingsCategoryButton.Disabled = false;
            worldSettingsCategoryButton.Disabled = registration is null;
            developerToggleButton.Disabled = true;
            ApplyResponsiveLayout();
        };

        menuQuitToMainButton.Text = "Quit to Menu";
        StyleButton(menuQuitToMainButton);
        menuQuitToMainButton.Pressed += () => quitToMenuConfirmation.PopupCentered(new Vector2I(470, 180));
        menuActions.AddChild(menuQuitToMainButton);

        quitGameConfirmation.Title = "Quit ClankerWorld?";
        quitGameConfirmation.DialogText = "Quit the game? Your committed world progress remains saved.";
        quitGameConfirmation.Confirmed += () => GetTree().Quit();
        AddChild(quitGameConfirmation);
        body.AddChild(menuActions);

        gameSettingsContent.AddThemeConstantOverride("separation", 8);
        worldSettingsContent.AddThemeConstantOverride("separation", 8);
        fullscreenToggle.Text = "Fullscreen";
        fullscreenToggle.ButtonPressed = DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen;
        fullscreenToggle.Toggled += SetFullscreen;
        gameSettingsContent.AddChild(fullscreenToggle);

        foreach (var preset in DisplaySizePresets)
        {
            var label = $"{preset.X} × {preset.Y}";
            windowSizeChoice.AddItem(label);
            renderResolutionChoice.AddItem(label);
        }
        windowSizeChoice.Selected = DisplaySizeIndex(GetWindow().Size);
        windowSizeChoice.Disabled = fullscreenToggle.ButtonPressed;
        windowSizeChoice.TooltipText = "Physical window dimensions in windowed mode. Fullscreen uses your display's size.";
        windowSizeChoice.ItemSelected += SetWindowSize;
        gameSettingsContent.AddChild(DisplaySettingRow("Window Size", windowSizeChoice));
        renderResolutionChoice.Selected = DisplaySizeIndex(GetWindow().ContentScaleSize);
        renderResolutionChoice.TooltipText = "Base size rendered by the game, scaled to fit the window or display.";
        renderResolutionChoice.ItemSelected += SetRenderResolution;
        gameSettingsContent.AddChild(DisplaySettingRow("Render Resolution", renderResolutionChoice));
        gameSettingsContent.AddChild(new Label
        {
            Text = "Render Resolution scales the whole game, including UI. Different aspect ratios use letterboxing.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });

        var clockFormatRow = new HBoxContainer();
        clockFormatRow.AddChild(new Label { Text = "Time display" });
        clockFormatChoice.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        clockFormatChoice.AddItem("24-hour", 0);
        clockFormatChoice.AddItem("12-hour (AM/PM)", 1);
        clockFormatChoice.Selected = displayPreferences.UseTwelveHourClock ? 1 : 0;
        clockFormatChoice.ItemSelected += SetClockFormat;
        clockFormatRow.AddChild(clockFormatChoice);
        gameSettingsContent.AddChild(clockFormatRow);

        var dateFormatRow = new HBoxContainer();
        dateFormatRow.AddChild(new Label { Text = "Date display" });
        dateFormatChoice.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        dateFormatChoice.AddItem("DD-MM-YYYY");
        dateFormatChoice.AddItem("MM-DD-YYYY");
        dateFormatChoice.AddItem("YYYY-MM-DD");
        dateFormatChoice.Selected = displayPreferences.DateFormat switch { "mdy" => 1, "ymd" => 2, _ => 0 };
        dateFormatChoice.ItemSelected += SetDateFormat;
        dateFormatRow.AddChild(dateFormatChoice);
        gameSettingsContent.AddChild(dateFormatRow);

        var lifePaceRow = new HBoxContainer();
        lifePaceRow.AddChild(new Label { Text = "Aging multiplier" });
        lifePaceChoice.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        lifePaceChoice.AddItem("Calendar", 1);
        lifePaceChoice.AddItem("Generations", 365);
        lifePaceChoice.AddItem("Fast generations", 1_460);
        lifePaceChoice.SetItemTooltip(0, "Original aging: one biological year per 365 world days.");
        lifePaceChoice.SetItemTooltip(1, "One biological year per world day (about 24 active minutes).");
        lifePaceChoice.SetItemTooltip(2, "One biological year per quarter-day (about 6 active minutes).");
        lifePaceChoice.TooltipText = "Prototype override only: changes future biological aging without changing the calendar, seasons or model-call speed. This is not the decided 40-day year or six-hour lifespan.";
        lifePaceRow.AddChild(lifePaceChoice);
        applyLifePaceButton.Text = "Apply";
        StyleButton(applyLifePaceButton);
        applyLifePaceButton.Pressed += () => _ = SaveLifePaceAsync();
        lifePaceRow.AddChild(applyLifePaceButton);
        var prototypePaceBody = new VBoxContainer();
        prototypePaceBody.AddChild(new Label
        {
            Text = "Experimental prototype control. The decided world calendar and lifespan are not implemented by this setting.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        prototypePaceBody.AddChild(lifePaceRow);
        developerBody.AddChild(NewPanel("Prototype aging override", prototypePaceBody));

        BuildAutosaveSettings();

        jevAssistanceToggle.Text = "Allow Jev assistance in this world";
        jevAssistanceToggle.TooltipText = "Jev is used only if configured. When off, work Jev would have handled goes to that agent's personal planning model, the world planner, or the local safe fallback. Memories and provider keys remain intact.";
        jevAssistanceToggle.Toggled += enabled => _ = SaveJevAssistanceAsync(enabled);
        worldSettingsContent.AddChild(jevAssistanceToggle);

        BuildCognitionSettingsPanel();
        worldSettingsContent.AddChild(cognitionSettingsPanel);

        BuildConnectionPanel();
        gameSettingsContent.AddChild(connectionPanel);
        BuildPairingPanel();
        gameSettingsContent.AddChild(pairingPanel);
        var settingsPages = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        settingsPages.AddChild(gameSettingsContent);
        settingsPages.AddChild(worldSettingsContent);
        worldSettingsContent.Hide();
        settingsScroll.CustomMinimumSize = new Vector2(0, 340);
        settingsScroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        settingsScroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        settingsScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        settingsScroll.AddChild(settingsPages);
        var settingsCategories = new VBoxContainer { CustomMinimumSize = new Vector2(130, 0) };
        gameSettingsCategoryButton.Text = "Game";
        StyleButton(gameSettingsCategoryButton);
        gameSettingsCategoryButton.Pressed += () => ShowSettingsSection(worldSpecific: false);
        settingsCategories.AddChild(gameSettingsCategoryButton);
        worldSettingsCategoryButton.Text = "World";
        StyleButton(worldSettingsCategoryButton);
        worldSettingsCategoryButton.Pressed += () => ShowSettingsSection(worldSpecific: true);
        settingsCategories.AddChild(worldSettingsCategoryButton);
        settingsCategories.AddChild(developerToggleButton);
        gameSettingsCategoryButton.Disabled = true;
        var settingsLayout = new HBoxContainer();
        settingsLayout.AddThemeConstantOverride("separation", 10);
        settingsLayout.AddChild(settingsCategories);
        settingsLayout.AddChild(settingsScroll);
        AddPanelContents(settingsPanel, "Settings", settingsLayout);
        settingsPanel.Hide();
        body.AddChild(settingsPanel);
        BuildModLibrary(body);

        developerScroll.CustomMinimumSize = new Vector2(0, 440);
        developerScroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        developerScroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        developerScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        developerBody.AddThemeConstantOverride("separation", 8);
        developerScroll.AddChild(developerBody);

        var retryBody = new VBoxContainer();
        pendingSubmissionLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        retryBody.AddChild(pendingSubmissionLabel);
        var retryButtons = new HBoxContainer();
        retryPendingSubmissionButton.Text = "Retry retained request";
        retryPendingSubmissionButton.Pressed += () => _ = RetryPendingSubmissionAsync();
        retryButtons.AddChild(retryPendingSubmissionButton);
        forgetPendingSubmissionButton.Text = "Forget retained request";
        forgetPendingSubmissionButton.Pressed += ForgetPendingSubmission;
        retryButtons.AddChild(forgetPendingSubmissionButton);
        retryBody.AddChild(retryButtons);
        developerBody.AddChild(NewPanel("Response-loss recovery · exact server retry", retryBody));
        RenderPendingSubmission();

        var authoringBody = new VBoxContainer();
        authoringKind.ItemSelected += _ => UpdateAuthoringHint();
        AddAuthoringKinds();
        authoringBody.AddChild(authoringKind);
        authoringId.PlaceholderText = "ID (resource/object/draft/asset as required)";
        authoringBody.AddChild(authoringId);
        authoringValue.PlaceholderText = "Value (terrain, kind, name, weather, digest…)";
        authoringBody.AddChild(authoringValue);
        authoringSecondaryValue.PlaceholderText = "Secondary value (season for set_weather_season)";
        authoringBody.AddChild(authoringSecondaryValue);
        var coordinateRow = new HBoxContainer();
        ConfigureCoordinate(authoringX, "x");
        ConfigureCoordinate(authoringY, "y");
        coordinateRow.AddChild(authoringX);
        coordinateRow.AddChild(authoringY);
        authoringRenewable.Text = "renewable resource";
        coordinateRow.AddChild(authoringRenewable);
        authoringBody.AddChild(coordinateRow);
        authoringHintLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        authoringBody.AddChild(authoringHintLabel);
        submitAuthoringButton.Text = "Apply one paused authoring operation";
        submitAuthoringButton.Pressed += () => _ = SubmitAuthoringAsync();
        authoringBody.AddChild(submitAuthoringButton);
        developerBody.AddChild(NewPanel("Paused authoring · server validates atomically", authoringBody));

        var deviceManagementBody = new VBoxContainer();
        pairingApprovalId.PlaceholderText = "Pending pairing ID from the new device";
        deviceManagementBody.AddChild(pairingApprovalId);
        pairingApprovalCode.PlaceholderText = "Six-digit comparison code";
        pairingApprovalCode.Secret = true;
        deviceManagementBody.AddChild(pairingApprovalCode);
        approvePairingButton.Text = "Approve paired device";
        approvePairingButton.Pressed += () => _ = ApprovePairingAsync();
        deviceManagementBody.AddChild(approvePairingButton);
        refreshDevicesButton.Text = "Refresh signed device list";
        refreshDevicesButton.Pressed += () => _ = RefreshDeviceRegistryAsync();
        deviceManagementBody.AddChild(refreshDevicesButton);
        pairedDeviceList.CustomMinimumSize = new Vector2(0, 104);
        pairedDeviceList.ItemSelected += index =>
        {
            var deviceId = pairedDeviceList.GetItemMetadata(checked((int)index)).AsString();
            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                revokeDeviceId.Text = deviceId;
            }
        };
        deviceManagementBody.AddChild(pairedDeviceList);
        revokeDeviceId.PlaceholderText = "Device ID to revoke";
        deviceManagementBody.AddChild(revokeDeviceId);
        revokeDeviceButton.Text = "Revoke other device";
        revokeDeviceButton.Pressed += () => _ = RevokeDeviceAsync();
        deviceManagementBody.AddChild(revokeDeviceButton);
        developerBody.AddChild(NewPanel("Paired-device management · signed server requests", deviceManagementBody));

        developerScroll.Hide();
        settingsLayout.AddChild(developerScroll);
        AddPanelContents(gameMenuPanel, body);
        gameMenuPanel.ZIndex = 100;
        gameMenuPanel.Hide();
        var menuCenter = new CenterContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 100,
        };
        menuCenter.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        content.AddChild(menuCenter);
        menuCenter.AddChild(gameMenuPanel);
        UpdateAuthoringHint();
    }

    private void BuildSelectedInhabitantCard()
    {
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 6);

        var heading = new HBoxContainer();
        selectedActorNameLabel.Text = string.Empty;
        selectedActorNameLabel.AddThemeFontSizeOverride("font_size", 18);
        selectedActorNameLabel.AddThemeColorOverride("font_color", new Color("F0F4EC"));
        selectedActorNameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        heading.AddChild(selectedActorNameLabel);
        clearSelectionButton.Text = "×";
        clearSelectionButton.TooltipText = "Close";
        StyleButton(clearSelectionButton);
        clearSelectionButton.Pressed += ClearInhabitantSelection;
        heading.AddChild(clearSelectionButton);
        body.AddChild(heading);

        body.AddChild(selectedAgentOverview);
        selectedAgentModelScroll.CustomMinimumSize = new Vector2(0, 300);
        selectedAgentModelScroll.AddChild(selectedAgentModelContent);
        var backToProfile = new Button { Text = "← Agent profile" };
        StyleButton(backToProfile);
        backToProfile.Pressed += CloseAgentModelEditor;
        selectedAgentModelContent.AddChild(backToProfile);
        body.AddChild(selectedAgentModelScroll);
        selectedAgentModelScroll.Hide();

        selectedActorSummaryLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        selectedActorSummaryLabel.Modulate = new Color("A7B9B7");
        selectedAgentOverview.AddChild(selectedActorSummaryLabel);
        selectedActorConditionLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        selectedActorConditionLabel.Modulate = new Color("C9DFCF");
        selectedAgentOverview.AddChild(selectedActorConditionLabel);

        var renameRow = new HBoxContainer();
        renameAgentInput.PlaceholderText = "Agent name";
        renameAgentInput.MaxLength = 48;
        renameAgentInput.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        renameRow.AddChild(renameAgentInput);
        renameAgentButton.Text = "Rename";
        StyleButton(renameAgentButton);
        renameAgentButton.Pressed += () => _ = RenameSelectedAgentAsync();
        renameRow.AddChild(renameAgentButton);
        selectedAgentOverview.AddChild(renameRow);

        ConfigureTextPanel(inhabitantDetails, 96);
        selectedAgentOverview.AddChild(inhabitantDetails);

        ConfigureTextPanel(inhabitantSocialDetails, 104);
        selectedAgentOverview.AddChild(inhabitantSocialDetails);

        ConfigureTextPanel(privateThoughtHistory, 86);
        privateThoughtHistory.TooltipText = "Only you can inspect these in-character thoughts. Other agents do not learn them automatically.";
        selectedAgentOverview.AddChild(privateThoughtHistory);

        memoriesButton.Text = "Memories";
        memoriesButton.TooltipText = "Inspect this agent's saved memories, including private memories and historical records after death.";
        StyleButton(memoriesButton);
        memoriesButton.Pressed += OpenMemories;
        var historyActions = new HBoxContainer();
        historyActions.AddChild(memoriesButton);

        familyTreeButton.Text = "Family Tree";
        familyTreeButton.TooltipText = "Inspect ancestry and partnerships, including deceased relatives.";
        StyleButton(familyTreeButton);
        familyTreeButton.Pressed += OpenFamilyTree;
        historyActions.AddChild(familyTreeButton);
        modelSettingsButton.Text = "Model and key";
        modelSettingsButton.TooltipText = "Choose this agent's personal model and saved or new API key.";
        StyleButton(modelSettingsButton);
        modelSettingsButton.Pressed += OpenAgentModelEditor;
        historyActions.AddChild(modelSettingsButton);
        selectedAgentOverview.AddChild(historyActions);

        var instructionHeading = new Label { Text = "Speak to them" };
        instructionHeading.AddThemeFontSizeOverride("font_size", 13);
        instructionHeading.AddThemeColorOverride("font_color", new Color("D8C6A5"));
        selectedAgentOverview.AddChild(instructionHeading);
        instructionKind.AddItem("Suggestion", 0);
        instructionKind.AddItem("Direct order", 1);
        instructionKind.CustomMinimumSize = new Vector2(0, 32);
        selectedAgentOverview.AddChild(instructionKind);
        instructionText.PlaceholderText = "Say something…";
        instructionText.CustomMinimumSize = new Vector2(0, 34);
        selectedAgentOverview.AddChild(instructionText);
        submitInstructionButton.Text = "Send";
        StyleButton(submitInstructionButton, primary: true);
        submitInstructionButton.Pressed += () => _ = SubmitInstructionAsync();
        selectedAgentOverview.AddChild(submitInstructionButton);

        AddPanelContents(selectedInhabitantCard, body);
        selectedInhabitantCard.CustomMinimumSize = new Vector2(350, 0);
        selectedInhabitantCard.ZIndex = 70;
        selectedInhabitantCard.Hide();
    }

    private void OpenAgentModelEditor()
    {
        if (selectedInhabitantId is null || registration is null || observationSession.Current is not { } current) return;
        PopulateCognitionTargets();
        for (var index = 1; index < cognitionTargetChoice.ItemCount; index++)
        {
            if (cognitionTargetChoice.GetItemMetadata(index).AsString() != selectedInhabitantId) continue;
            cognitionTargetChoice.Select(index);
            PopulateProviderChoices(ActiveProviderForSelectedRole());
            PopulateCredentialChoices();
            cognitionTargetChoice.Hide();
            cognitionSettingsPanel.Reparent(selectedAgentModelContent, keepGlobalTransform: false);
            selectedAgentOverview.Hide();
            selectedAgentModelScroll.Show();
            RenderProviderConfiguration();
            PositionSelectedInhabitantCard(current.Baseline.Snapshot);
            _ = RefreshProviderConfigurationAsync();
            return;
        }
        SetStatus("This agent is not available for model configuration", good: false);
    }

    private void CloseAgentModelEditor()
    {
        if (!selectedAgentModelScroll.Visible) return;
        selectedAgentModelScroll.Hide();
        cognitionSettingsPanel.Reparent(worldSettingsContent, keepGlobalTransform: false);
        cognitionTargetChoice.Show();
        selectedAgentOverview.Show();
        cognitionApiKeyInput.Text = string.Empty;
        cognitionCredentialLabelInput.Text = string.Empty;
    }

    private void BuildStatusToast(Control content)
    {
        statusLabel.Text = "Connecting…";
        statusLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        statusLabel.HorizontalAlignment = HorizontalAlignment.Center;
        statusLabel.CustomMinimumSize = new Vector2(320, 0);
        AddPanelContents(statusToast, statusLabel);
        statusToast.ZIndex = 120;
        statusToast.Hide();
        content.AddChild(statusToast);
    }

    private void ToggleInhabitants()
    {
        var show = !rosterPanel.Visible;
        familyTreePanel.Hide();
        settlementPanel.Hide();
        eventsPanel.Hide();
        worldOverviewPanel.Hide();
        worldInfoPanel.Hide();
        rosterPanel.Visible = show;
    }

    private void ToggleEvents()
    {
        var show = !eventsPanel.Visible;
        familyTreePanel.Hide();
        settlementPanel.Hide();
        rosterPanel.Hide();
        worldOverviewPanel.Hide();
        worldInfoPanel.Hide();
        eventsPanel.Visible = show;
    }

    private void ToggleWorldInfo()
    {
        var show = !worldInfoPanel.Visible;
        familyTreePanel.Hide();
        settlementPanel.Hide();
        rosterPanel.Hide();
        eventsPanel.Hide();
        worldOverviewPanel.Hide();
        worldInfoPanel.Visible = show;
    }

    private void OpenFamilyTree()
    {
        if (selectedInhabitantId is not { } id || observationSession.Current is not { } current)
            return;
        ShowFamilyTree(current.Baseline.Snapshot, id);
    }

    private void ShowFamilyTree(OwnerWorldSnapshot snapshot, string id)
    {
        memoriesPanel.Hide();
        familyTreeView.SetPeople(snapshot.WorldId, snapshot.Inhabitants, id);
        UpdateFamilyTreeStatus();
        rosterPanel.Hide();
        eventsPanel.Hide();
        worldOverviewPanel.Hide();
        worldInfoPanel.Hide();
        settlementPanel.Hide();
        familyTreePanel.Show();
        ApplyResponsiveLayout();
    }

    private void UpdateFamilyTreeStatus()
    {
        familyTreeStatus.Text = familyTreeView.ParentEdgeCount + familyTreeView.PartnerEdgeCount == 0
            ? "No family links recorded yet. Housemates are not automatically relatives."
            : "Green: parent–child   ·   Pink: partnership   ·   Click a person to inspect";
    }

    private void SelectFromFamilyTree(string id)
    {
        familyTreePanel.Hide();
        selectedInhabitantId = id;
        if (observationSession.Current is not { } current) return;
        var snapshot = current.Baseline.Snapshot;
        RenderInhabitantList(snapshot);
        RenderInhabitantDetails(snapshot);
        RenderSelectedInhabitantCard(snapshot);
        RenderMap(snapshot);
    }

    private void OpenMemories()
    {
        if (selectedInhabitantId is null) return;
        familyTreePanel.Hide();
        rosterPanel.Hide();
        eventsPanel.Hide();
        worldOverviewPanel.Hide();
        worldInfoPanel.Hide();
        settlementPanel.Hide();
        memoriesPanel.Show();
        ApplyResponsiveLayout();
    }

    private async Task TogglePauseAsync()
    {
        var paused = observationSession.Current?.Baseline.Snapshot.Authoring?.IsPaused == true;
        await SetPausedAsync(!paused);
    }

    private async Task ToggleGameMenuAsync()
    {
        if (gameMenuPanel.Visible)
        {
            await CloseGameMenuAsync();
            return;
        }

        rosterPanel.Hide();
        eventsPanel.Hide();
        familyTreePanel.Hide();
        memoriesPanel.Hide();
        returnToMainMenu = false;
        menuResumeButton.Text = "Resume";
        SetWorldMenuActionsVisible(true);
        menuHeadingLabel.Text = "Paused";
        settlementPanel.Hide();
        gameMenuPanel.Show();
        menuShade.Show();
        ApplyResponsiveLayout();

        var paused = observationSession.Current?.Baseline.Snapshot.Authoring?.IsPaused == true;
        menuPausedWorld = observationSession.Current is not null && !paused;
        if (menuPausedWorld)
        {
            await SetPausedAsync(paused: true);
        }
    }

    private async Task CloseGameMenuAsync()
    {
        if (returnToMainMenu)
        {
            CloseGameMenu();
            ShowMainMenu();
            return;
        }
        var resumeWorld = menuPausedWorld;
        CloseGameMenu();
        if (resumeWorld)
        {
            await SetPausedAsync(paused: false);
        }
    }

    private void CloseGameMenu()
    {
        cognitionApiKeyInput.Text = string.Empty;
        gameMenuPanel.Hide();
        menuShade.Hide();
        settingsPanel.Hide();
        modLibraryPanel.Hide();
        developerScroll.Hide();
        menuPausedWorld = false;
    }

    private void OpenMenuForSetup()
    {
        returnToMainMenu = true;
        mainMenuOverlay.Hide();
        menuPausedWorld = false;
        menuResumeButton.Text = "Back to Main Menu";
        SetWorldMenuActionsVisible(false);
        menuHeadingLabel.Text = "Set up your world";
        gameMenuPanel.Show();
        menuShade.Show();
        ShowSettingsSection(worldSpecific: false);
    }

    private void SetFullscreen(bool enabled)
    {
        DisplayServer.WindowSetMode(enabled
            ? DisplayServer.WindowMode.Fullscreen
            : DisplayServer.WindowMode.Windowed);
        windowSizeChoice.Disabled = enabled;
        if (!enabled)
            GetWindow().Size = DisplaySizePresets[windowSizeChoice.Selected];
    }

    private void ApplySavedDisplaySettings()
    {
        var window = GetWindow();
        var windowSize = new Vector2I(displayPreferences.WindowWidth, displayPreferences.WindowHeight);
        var renderSize = new Vector2I(displayPreferences.RenderWidth, displayPreferences.RenderHeight);
        window.ContentScaleMode = Window.ContentScaleModeEnum.Viewport;
        window.ContentScaleAspect = Window.ContentScaleAspectEnum.Keep;
        window.ContentScaleSize = DisplaySizePresets[DisplaySizeIndex(renderSize)];
        if (DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Windowed)
            window.Size = DisplaySizePresets[DisplaySizeIndex(windowSize)];
    }

    private static int DisplaySizeIndex(Vector2I size)
    {
        for (var index = 0; index < DisplaySizePresets.Length; index++)
            if (DisplaySizePresets[index] == size) return index;
        return 0;
    }

    private static HBoxContainer DisplaySettingRow(string label, OptionButton choice)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(135, 0) });
        choice.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(choice);
        return row;
    }

    private void SetWindowSize(long index)
    {
        var size = DisplaySizePresets[(int)index];
        SaveDisplayPreferences(displayPreferences with { WindowWidth = size.X, WindowHeight = size.Y });
        if (!fullscreenToggle.ButtonPressed)
            GetWindow().Size = size;
    }

    private void SetRenderResolution(long index)
    {
        var size = DisplaySizePresets[(int)index];
        GetWindow().ContentScaleSize = size;
        SaveDisplayPreferences(displayPreferences with { RenderWidth = size.X, RenderHeight = size.Y });
    }

    private void SetClockFormat(long index)
    {
        SaveDisplayPreferences(displayPreferences with { UseTwelveHourClock = index == 1 });
        if (observationSession.Current is { } current)
            Render(current.Baseline.Snapshot, []);
    }

    private void SetDateFormat(long index)
    {
        var format = index switch { 1 => "mdy", 2 => "ymd", _ => "dmy" };
        SaveDisplayPreferences(displayPreferences with { DateFormat = format });
        if (observationSession.Current is { } current)
            Render(current.Baseline.Snapshot, []);
    }

    private void SaveDisplayPreferences(GameDisplayPreferences updated)
    {
        displayPreferences = updated;
        try
        {
            displayPreferencesStore.Save(displayPreferences);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            SetStatus("could not save the game display preferences", good: false);
        }
    }

    private string DisplayWorldClock(long worldTick) =>
        GameUiText.FormatWorldClock(worldTick, displayPreferences.UseTwelveHourClock,
            observedCalendarPace, displayPreferences.DateFormat);

    private void AddAuthoringKinds()
    {
        AddAuthoringKind("set_terrain", "Set terrain — value: meadow, water, or mountain; x/y required");
        AddAuthoringKind("place_resource", "Place resource — ID, value=kind, x/y, renewable required");
        AddAuthoringKind("remove_resource", "Remove resource — ID required");
        AddAuthoringKind("place_object", "Place object — ID, value=kind, x/y required");
        AddAuthoringKind("remove_object", "Remove object — ID required");
        AddAuthoringKind("place_building", "Place building — ID, value=building kind, x/y required");
        AddAuthoringKind("remove_building", "Remove building — ID required");
        AddAuthoringKind("place_plant", "Place plant — ID, value=plant kind, x/y required");
        AddAuthoringKind("remove_plant", "Remove plant — ID required");
        AddAuthoringKind("create_founder_draft", "Create founder draft — ID, value=display name, x/y required");
        AddAuthoringKind("remove_founder_draft", "Remove founder draft — ID required");
        AddAuthoringKind("set_weather", "Set weather — value required");
        AddAuthoringKind("set_season", "Set season — value: spring, summer, autumn, or winter");
        AddAuthoringKind("set_weather_season", "Set weather + season — value=weather, secondary value=season");
        AddAuthoringKind("add_approved_asset_reference", "Add approved asset reference — ID and exact lowercase sha256 digest must already exist in the host catalog");
        AddAuthoringKind("remove_approved_asset_reference", "Remove approved asset reference — ID required");
    }

    private void AddAuthoringKind(string kind, string description)
    {
        authoringKind.AddItem(kind);
        authoringKind.SetItemMetadata(authoringKind.ItemCount - 1, description);
    }

    private void UpdateAuthoringHint()
    {
        if (authoringKind.ItemCount == 0)
        {
            return;
        }

        authoringHintLabel.Text = authoringKind.GetItemMetadata(authoringKind.Selected).AsString();
    }

    private static void ConfigureCoordinate(SpinBox box, string placeholder)
    {
        box.MinValue = 0;
        box.MaxValue = 99;
        box.Step = 1;
        box.CustomMinimumSize = new Vector2(72, 0);
        box.TooltipText = placeholder;
    }

    private void Render(OwnerWorldSnapshot snapshot, IReadOnlyList<OwnerWorldEvent> appendedEvents)
    {
        if (usagePauseWorldId != snapshot.WorldId)
        {
            usagePauseWorldId = snapshot.WorldId;
            wasObservedPaused = false;
        }
        var isPaused = snapshot.Authoring?.IsPaused == true;
        var checkUsagePause = isPaused && !wasObservedPaused;
        wasObservedPaused = isPaused;
        observedCalendarPace = snapshot.CalendarPace;
        jevAssistanceToggle.SetPressedNoSignal(snapshot.JevEnabled == true);
        if (cameraWorldId is not null && cameraWorldId != snapshot.WorldId)
        {
            knownEvents.Clear();
            familyTreePanel.Hide();
            memoriesPanel.Hide();
            ClearTileSelection();
        }
        foreach (var worldEvent in appendedEvents)
        {
            knownEvents[worldEvent.EventId] = worldEvent;
        }
        foreach (var expiredId in knownEvents.Keys.OrderByDescending(id => id).Skip(2048).ToArray())
        {
            knownEvents.Remove(expiredId);
        }

        RenderInhabitantList(snapshot);
        RenderMap(snapshot);
        RenderWorldHud(snapshot);
        if (checkUsagePause && registration is not null) _ = ObserveUsagePauseAsync();
        RenderFounderSetup(snapshot);
        RenderWorldInfo(snapshot);
        RenderInhabitantDetails(snapshot);
        RenderSelectedInhabitantCard(snapshot);
        if (familyTreePanel.Visible && selectedInhabitantId is { } center)
        {
            familyTreeView.SetPeople(snapshot.WorldId, snapshot.Inhabitants, center);
            UpdateFamilyTreeStatus();
        }
        RenderWorldDetails(snapshot);
        RenderModLibrary(snapshot);
        RenderEventLog();
        RefreshControlAvailability();
    }

    private static bool HasMap(OwnerWorldSnapshot snapshot) =>
        snapshot.PackedTerrain is not null || snapshot.Tiles.Count > 0;

    private static (int Width, int Height) MapDimensions(OwnerWorldSnapshot snapshot) =>
        snapshot.PackedTerrain is { } packed ? (packed.Width, packed.Height) :
        snapshot.Tiles.Count == 0 ? (0, 0) :
        (snapshot.Tiles.Max(tile => tile.X) + 1, snapshot.Tiles.Max(tile => tile.Y) + 1);

    private static bool MapContains(OwnerWorldSnapshot snapshot, int x, int y)
    {
        var (width, height) = MapDimensions(snapshot);
        return x >= 0 && y >= 0 && x < width && y < height;
    }

    private void RenderMap(OwnerWorldSnapshot snapshot)
    {
        renderedMapSnapshot = snapshot;
        var objectIds = snapshot.Resources.Where(resource => resource.TreeKind is null)
            .Select(resource => "resource:" + resource.Id)
            .Concat(snapshot.Objects.Select(item => "object:" + item.Id))
            .Concat(snapshot.PlacedBuildings.Select(item => "building:" + item.InstanceId)).ToHashSet(StringComparer.Ordinal);
        foreach (var id in mapObjectVisuals.Keys.Where(id => !objectIds.Contains(id)).ToArray())
        {
            mapObjectVisuals[id].QueueFree();
            mapObjectVisuals.Remove(id);
            mapObjectCanonicalXs.Remove(id);
        }

        if (!HasMap(snapshot))
        {
            foreach (var visual in inhabitantVisuals.Values) visual.QueueFree();
            inhabitantVisuals.Clear();
            inhabitantCanonicalXs.Clear();
            terrainLayer.SetHoveredTile(null);
            return;
        }

        var manifest = snapshot.Authoring?.CurrentMapManifestDigest ?? snapshot.MapManifestDigest;
        if (terrainMap is null || !string.Equals(terrainWorldId, snapshot.WorldId, StringComparison.Ordinal) ||
            !string.Equals(terrainManifestDigest, manifest, StringComparison.Ordinal) ||
            !string.Equals(terrainLayersDigest, snapshot.MapLayersDigest, StringComparison.Ordinal) ||
            (!terrainMap.HasMapLayers && snapshot.PackedMapLayers is not null))
        {
            var (width, height) = MapDimensions(snapshot);
            terrainMap = snapshot.PackedTerrain is { } packed
                ? WorldTerrainMap.FromPacked(packed, snapshot.PackedMapLayers)
                : WorldTerrainMap.FromTiles(snapshot.Tiles, width, height, snapshot.PackedMapLayers);
            terrainWorldId = snapshot.WorldId;
            terrainManifestDigest = manifest;
            terrainLayersDigest = snapshot.MapLayersDigest;
            terrainLayer.SetWorld(terrainMap);
            worldOverview.SetWorld(terrainMap);
        }
        terrainLayer.SetTrees(snapshot.Resources);
        terrainLayer.SetWeatherRegions(snapshot.WeatherRegionSize, snapshot.WeatherRegions);
        var mapWidth = terrainMap.Width;
        var mapHeight = terrainMap.Height;
        worldOverview.WrapsEastWest = snapshot.WrapsEastWest;
        if (!string.Equals(cameraWorldId, snapshot.WorldId, StringComparison.Ordinal))
        {
            cameraWorldId = snapshot.WorldId;
            cameraZoom = 1;
            cameraCenterTiles = new Vector2(mapWidth / 2f, mapHeight / 2f);
        }
        UpdateMapGeometry(snapshot);

        foreach (var resource in snapshot.Resources)
        {
            if (resource.TreeKind is not null) continue;
            AddMapObjectVisual(
                "resource:" + resource.Id,
                resource.Position,
                ResourceGlyph(resource.Kind),
                ResourceMarker(resource.Kind) + (resource.Quantity is null ? "" : " " + GameUiText.ResourceQuantity(resource.Kind, resource.Quantity, resource.Capacity)),
                GameUiText.ResourceTooltip(resource));
        }

        foreach (var mapObject in snapshot.Objects)
        {
            AddMapObjectVisual(
                "object:" + mapObject.Id,
                mapObject.Position,
                ObjectGlyph(mapObject.Kind),
                ObjectMarker(mapObject.Kind),
                Pretty(mapObject.Kind));
        }

        foreach (var building in snapshot.PlacedBuildings)
        {
            var tags = building.Tags ?? [];
            var kind = tags.Contains("shelter", StringComparer.Ordinal) ? "shelter" :
                tags.Any(tag => tag is "warmth" or "cooking") ? "campfire" : "building";
            var name = building.DisplayName ?? "Building";
            AddMapObjectVisual("building:" + building.InstanceId, building.Position, ObjectGlyph(kind), name,
                $"{name}\nBuilt · {building.Width} × {building.Height} tiles", building.Width, building.Height);
        }

        foreach (var group in snapshot.Inhabitants
            .Where(inhabitant => !inhabitant.IsDraft && string.Equals(inhabitant.Lifecycle, "active", StringComparison.OrdinalIgnoreCase))
            .GroupBy(inhabitant => PositionKey(inhabitant.Position)))
        {
            var occupants = group.ToArray();
            var columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(occupants.Length)));
            var rows = (int)Math.Ceiling((double)occupants.Length / columns);
            var cellWidth = (float)currentTileSize / columns;
            var cellHeight = (float)currentTileSize / rows;
            for (var index = 0; index < occupants.Length; index++)
            {
                var inhabitant = occupants[index];
                var stride = currentTileSize + TileGap;
                var inset = Math.Min(3f, Math.Min(cellWidth, cellHeight) / 8f);
                var visualLimit = Math.Min(78f, currentTileSize * 0.55f);
                var markerSize = new Vector2(Math.Min(cellWidth - 2 * inset, visualLimit),
                    Math.Min(cellHeight - 2 * inset, visualLimit));
                var offsetX = (index % columns) * cellWidth + (cellWidth - markerSize.X) / 2;
                var offsetY = (index / columns) * cellHeight + (cellHeight - markerSize.Y) / 2;
                var targetPosition = new Vector2(
                    inhabitant.Position.X * stride + offsetX,
                    inhabitant.Position.Y * stride + offsetY);
                if (!inhabitantVisuals.TryGetValue(inhabitant.Id, out var actorMarker))
                {
                    actorMarker = new AgentMarker
                    {
                        Position = targetPosition,
                        ZIndex = 10,
                    };
                    actorMarker.Activated += () => SelectInhabitant(inhabitant.Id);
                    actorMarker.MouseEntered += RefreshTileHoverAtMouse;
                    actorMarker.MouseExited += RefreshTileHoverAtMouse;
                    entityLayer.AddChild(actorMarker);
                    inhabitantVisuals.Add(inhabitant.Id, actorMarker);
                }
                actorMarker.Caption = $"{ActivityGlyph(inhabitant.PublicIntention?.CandidateId)} {ActorLabel(inhabitant.DisplayName)}";
                var actorTooltip = $"{inhabitant.DisplayName} · {Pretty(inhabitant.Lifecycle)} · " +
                    (inhabitant.PublicIntention?.Summary ?? "taking in the world");
                if (actorMarker.TooltipText != actorTooltip) actorMarker.TooltipText = actorTooltip;
                actorMarker.Selected = string.Equals(inhabitant.Id, selectedInhabitantId, StringComparison.Ordinal);
                inhabitantCanonicalXs[inhabitant.Id] = targetPosition.X;
                actorMarker.Position = new Vector2(
                    WrappedMarkerX(targetPosition.X, mapWidth, stride, snapshot.WrapsEastWest),
                    targetPosition.Y);
                actorMarker.Size = markerSize;

            }
        }

        var visibleInhabitantIds = snapshot.Inhabitants
            .Where(inhabitant => !inhabitant.IsDraft && string.Equals(inhabitant.Lifecycle, "active", StringComparison.OrdinalIgnoreCase))
            .Select(inhabitant => inhabitant.Id)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var removedId in inhabitantVisuals.Keys.Where(id => !visibleInhabitantIds.Contains(id)).ToArray())
        {
            inhabitantVisuals[removedId].QueueFree();
            inhabitantVisuals.Remove(removedId);
            inhabitantCanonicalXs.Remove(removedId);
        }

        RenderTileInspection(snapshot);
        PositionSelectedInhabitantCard(snapshot);
        RefreshTileHoverAtMouse();
    }

    private void AddMapObjectVisual(
        string id,
        OwnerWorldPosition position,
        string glyph,
        string label,
        string tooltip,
        int width = 1,
        int height = 1)
    {
        var stride = currentTileSize + TileGap;
        if (!mapObjectVisuals.TryGetValue(id, out var visual))
        {
            visual = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Pass,
                ZIndex = 5,
                ClipText = true,
            };
            visual.AddThemeFontSizeOverride("font_size", 12);
            visual.AddThemeColorOverride("font_color", new Color("E8F0D8"));
            visual.AddThemeColorOverride("font_shadow_color", new Color("18211D"));
            visual.AddThemeConstantOverride("shadow_offset_x", 1);
            visual.AddThemeConstantOverride("shadow_offset_y", 1);
            objectLayer.AddChild(visual);
            mapObjectVisuals.Add(id, visual);
        }
        visual.Text = $"{glyph}\n{label}";
        var canonicalX = position.X * stride + 4;
        mapObjectCanonicalXs[id] = canonicalX;
        visual.Position = new Vector2(WrappedMarkerX(canonicalX, terrainMap!.Width, stride,
            renderedMapSnapshot?.WrapsEastWest == true), position.Y * stride + 4);
        visual.Size = new Vector2(stride * Math.Clamp(width, 1, 32) - TileGap - 8,
            stride * Math.Clamp(height, 1, 32) - TileGap - 8);
        visual.TooltipText = tooltip;
    }

    private void RenderWorldHud(OwnerWorldSnapshot snapshot)
    {
        var paused = snapshot.Authoring?.IsPaused == true;
        clockLabel.Text = DisplayWorldClock(snapshot.WorldTick);
        inhabitantsButton.Text = $"Agents {LivingPopulation(snapshot)}";
        inhabitantsButton.TooltipText = "Living agents · open the inhabitant list";
        climateLabel.Text = snapshot.Authoring is { } authoring
            ? $"{Pretty(authoring.Season)} · {Pretty(WeatherAtCamera(snapshot))}"
            : string.Empty;
        pauseButton.Text = paused ? "Play" : "Pause";
        pauseButton.TooltipText = paused ? "Resume the world" : "Pause the world";
        menuResumeButton.Text = menuPausedWorld ? "Resume" : "Close menu";
    }

    private static int LivingPopulation(OwnerWorldSnapshot snapshot) => snapshot.Inhabitants.Count(inhabitant =>
        !inhabitant.IsDraft && string.Equals(inhabitant.Lifecycle, "active", StringComparison.OrdinalIgnoreCase));

    private OwnerWeatherRegion? WeatherRegionAtCamera(OwnerWorldSnapshot snapshot)
    {
        var size = Math.Max(1, snapshot.WeatherRegionSize);
        var x = Math.Max(0, (int)MathF.Floor(cameraCenterTiles.X / size));
        var y = Math.Max(0, (int)MathF.Floor(cameraCenterTiles.Y / size));
        return snapshot.WeatherRegions.FirstOrDefault(region => region.X == x && region.Y == y);
    }

    private string WeatherAtCamera(OwnerWorldSnapshot snapshot) =>
        WeatherRegionAtCamera(snapshot)?.Weather ?? snapshot.Authoring?.Weather ?? "unknown";

    private void RenderWorldInfo(OwnerWorldSnapshot snapshot)
    {
        var (width, height) = MapDimensions(snapshot);
        var localWeather = snapshot.Authoring is { } authoring
            ? $"{Pretty(authoring.Season)} · {Pretty(WeatherAtCamera(snapshot))}"
            : "Not reported";
        worldInfoText.Text =
            $"Date and time: {DisplayWorldClock(snapshot.WorldTick)}\n" +
            (snapshot.CalendarPace is { } pace ? $"Calendar: {pace.DaysPerYear} days/year\n" : "") +
            $"Living agents: {LivingPopulation(snapshot)}\n" +
            $"Map: {width} × {height} tiles\n" +
            $"Buildings: {snapshot.PlacedBuildings.Count}\n" +
            $"Resource sites: {snapshot.Resources.Count}\n" +
            $"Season and weather at camera: {localWeather}" +
            (WeatherRegionAtCamera(snapshot)?.SoilMoisture is { } moisture
                ? $"\nSoil moisture nearby: {moisture}/100"
                : "");
    }

    private void RenderInhabitantList(OwnerWorldSnapshot snapshot)
    {
        var previousSelection = selectedInhabitantId;
        var selectionFound = false;
        inhabitantList.Clear();
        var inhabitants = snapshot.Inhabitants
            .Where(inhabitant => !inhabitant.IsDraft)
            .OrderBy(inhabitant => inhabitant.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var living = inhabitants.Count(inhabitant => string.Equals(inhabitant.Lifecycle, "active", StringComparison.OrdinalIgnoreCase));
        var deceased = inhabitants.Length - living;
        rosterSummaryLabel.Text = inhabitants.Length == 0
            ? "No one lives here yet."
            : deceased == 0 ? $"{living} living" : $"{living} living · {deceased} deceased";

        for (var index = 0; index < inhabitants.Length; index++)
        {
            var inhabitant = inhabitants[index];
            inhabitantList.AddItem($"{inhabitant.DisplayName}   ·   {Pretty(inhabitant.Lifecycle)}");
            inhabitantList.SetItemMetadata(index, inhabitant.Id);
            if (string.Equals(inhabitant.Id, previousSelection, StringComparison.Ordinal))
            {
                selectionFound = true;
                inhabitantList.Select(index);
            }
        }

        if (!selectionFound)
        {
            selectedInhabitantId = null;
            inhabitantList.DeselectAll();
        }
    }

    private void RenderInhabitantDetails(OwnerWorldSnapshot snapshot)
    {
        var inhabitant = snapshot.Inhabitants.FirstOrDefault(item =>
            string.Equals(item.Id, selectedInhabitantId, StringComparison.Ordinal));
        inhabitantDetails.Clear();
        if (inhabitant is null)
        {
            return;
        }

        if (string.Equals(inhabitant.Lifecycle, "dead", StringComparison.OrdinalIgnoreCase))
        {
            inhabitantDetails.AppendText("Deceased · historical record; no current activity or carried inventory.");
            return;
        }

        var inventory = inhabitant.Inventory.Count == 0
            ? "none"
            : string.Join(", ", inhabitant.Inventory.Select(item => $"{item.Kind}: {item.Quantity}"));
        var currentActivity = string.IsNullOrWhiteSpace(inhabitant.Route.Status)
            ? "wandering"
            : Pretty(inhabitant.Route.Status);
        var destination = inhabitant.Route.Destination is { } routeDestination
            ? $" toward {routeDestination.X}, {routeDestination.Y}"
            : string.Empty;
        inhabitantDetails.AppendText(
            $"{currentActivity}{destination}\n" +
            $"Hunger {NeedPercent(inhabitant.HungerBasisPoints)}%\n" +
            $"Carrying {inventory}");
    }

    private void RenderSelectedInhabitantCard(OwnerWorldSnapshot snapshot)
    {
        var inhabitant = snapshot.Inhabitants.FirstOrDefault(item =>
            string.Equals(item.Id, selectedInhabitantId, StringComparison.Ordinal));
        if (inhabitant is null)
        {
            CloseAgentModelEditor();
            selectedActorNameLabel.Text = string.Empty;
            renamingAgentId = null;
            selectedActorSummaryLabel.Text = string.Empty;
            selectedActorConditionLabel.Text = string.Empty;
            inhabitantSocialDetails.Clear();
            privateThoughtHistory.Clear();
            memoryHistory.Clear();
            memoriesPanel.Hide();
            selectedInhabitantCard.Hide();
            return;
        }

        selectedActorNameLabel.Text = inhabitant.DisplayName;
        if (renamingAgentId != inhabitant.Id || !renameAgentInput.HasFocus())
        {
            renameAgentInput.Text = inhabitant.DisplayName;
            renamingAgentId = inhabitant.Id;
        }
        var ageBand = inhabitant.DecisionFactors.FirstOrDefault(factor => factor.Key == "age-band")?.Detail;
        var ageYears = inhabitant.DecisionFactors.FirstOrDefault(factor => factor.Key == "age-years")?.Detail;
        var ageDays = inhabitant.DecisionFactors.FirstOrDefault(factor => factor.Key == "age-days")?.Detail;
        var deathTick = inhabitant.DecisionFactors.FirstOrDefault(factor => factor.Key == "death-tick")?.Detail;
        var deathCause = inhabitant.DecisionFactors.FirstOrDefault(factor => factor.Key == "death-cause")?.Detail;
        var willStatus = inhabitant.DecisionFactors.FirstOrDefault(factor => factor.Key == "will-status")?.Detail;
        var willHeir = inhabitant.DecisionFactors.FirstOrDefault(factor => factor.Key == "will-heir")?.Detail;
        var isDeceased = string.Equals(inhabitant.Lifecycle, "dead", StringComparison.OrdinalIgnoreCase);
        modelSettingsButton.Disabled = isDeceased || registration is null;
        if (selectedAgentModelScroll.Visible && SelectedCognitionTarget() != inhabitant.Id)
            CloseAgentModelEditor();
        var waitingForDecision = inhabitant.DecisionFactors.Any(factor => factor.Key == "decision-pending");
        selectedActorSummaryLabel.Text = Pretty(inhabitant.Lifecycle) + (ageBand is null ? "" : " · " + Pretty(ageBand)) +
            (ageYears is null ? "" : " · " + ageYears + " years") +
            (ageDays is null ? "" : " · " + ageDays + " days") +
            (deathTick is not null && long.TryParse(deathTick, CultureInfo.InvariantCulture, out var finalTick)
                ? $" · {DisplayWorldClock(finalTick)}" : "");
        var intention = isDeceased
            ? $"Life ended{(deathCause is null ? "" : " · " + Pretty(deathCause))}. No current thoughts or activity." +
              (willStatus == "accepted" ? $" Final will: personal estate to {willHeir}." :
                  willStatus == "pending" ? " Final will pending." :
                  willStatus == "default" ? " Personal estate follows household inheritance." : "")
            : waitingForDecision
            ? "Decision pending."
            : inhabitant.PublicIntention is { } publicIntention
            ? $"Wants to {GameUiText.HumanizeIdentifier(publicIntention.Summary).ToLowerInvariant()}."
            : "Taking in their surroundings.";
        var relationships = inhabitant.Relationships.Count == 0
            ? "No close relationships yet."
            : string.Join(
                "; ",
                inhabitant.Relationships.Select(relationship =>
                {
                    var other = snapshot.Inhabitants.FirstOrDefault(item => item.Id == relationship.OtherPartyId)?.DisplayName
                        ?? Pretty(relationship.OtherPartyId);
                    return $"{Pretty(relationship.Type)} with {other} · {Pretty(relationship.State)}";
                }));
        var decision = snapshot.Cognition?.Decisions?.FirstOrDefault(item => item.InhabitantId == inhabitant.Id);
        var activity = waitingForDecision ? "Decision pending" : decision is null
            ? "No decision yet"
            : $"{Pretty(decision.Provider)}{(decision.FellBack ? " (fallback)" : "")} · {GameUiText.HumanizeIdentifier(decision.CandidateId)}";
        var projectText = inhabitant.Project is { } project
            ? $"{project.Label} · {Pretty(project.Stage)} · {project.WorkDone}/{project.WorkRequired}" +
                (project.Blocker is null ? "" : $"\n{project.Blocker}")
            : "No settlement project";
        var socialNotes = inhabitant.SocialNotes.Count == 0 ? "" : "\n" + string.Join("\n", inhabitant.SocialNotes);
        var standing = inhabitant.SocialStanding.Count == 0 ? "" : "\n" + string.Join(" · ",
            inhabitant.SocialStanding.Select(item => $"Trust in {item.SubjectName} {item.Trust}/10"));
        var condition = inhabitant.Survival is { } survival
            ? $"{(isDeceased ? "At death · " : "")}Warmth {survival.WarmthBasisPoints / 100}% · Illness {survival.IllnessBasisPoints / 100}%" +
                $" · Diet {survival.NutritionBasisPoints / 100}%\n" +
                $"{(survival.HasClothing ? "Clothed" : "No warm clothing")} · {(survival.HasTool ? "Tool equipped" : "Working by hand")}" :
                "Condition data unavailable";
        selectedActorConditionLabel.Text = condition;
        var role = inhabitant.DecisionFactors.FirstOrDefault(factor => factor.Key == "role")?.Detail;
        var learning = inhabitant.Lesson is { } lesson
            ? $"\nLearning {Pretty(lesson.Role)} with {lesson.TeacherName} · {Pretty(lesson.Stage)} · {lesson.Progress}/{lesson.Required}" : "";
        if (inhabitant.Proficiency is { } practice)
            learning += $"\nPractice · Building {practice.Building}/30 · Farming {practice.Farming}/30 · Crafting {practice.Crafting}/30";
        var socialText = $"{(role is null ? "" : Pretty(role) + "\n")}{(inhabitant.Project is null ? intention : projectText)}{learning}\n{relationships}{standing}{socialNotes}\n{activity}";
        if (inhabitantSocialDetails.Text != socialText) inhabitantSocialDetails.Text = socialText;
        var thoughtHeading = isDeceased ? "Private thoughts · historical" : "Private thoughts";
        privateThoughtHistory.Text = inhabitant.RecentPrivateThoughts.Count == 0
            ? thoughtHeading + "\nNone recorded yet."
            : thoughtHeading + "\n" + string.Join("\n", inhabitant.RecentPrivateThoughts
                .Reverse().Select(thought => $"{DisplayWorldClock(thought.WorldTick)}  {thought.Text}"));
        memoryHistory.Text = inhabitant.RecentMemories.Count == 0
            ? "No saved memories for this agent yet."
            : string.Join("\n\n", inhabitant.RecentMemories.Select(memory =>
                $"{DisplayWorldClock(memory.WorldTick)} · {Pretty(memory.Visibility)} · about {memory.SubjectName}\n{memory.Summary}"));
        inhabitantSocialDetails.TooltipText = decision is null ? "" :
            $"Last accepted decision\nRole: {decision.Role ?? "not reported"}\nModel: {decision.Model ?? "not reported"}\nConfidence: {decision.Confidence:P0}\n" +
            $"Latency: {decision.LatencyMilliseconds?.ToString(CultureInfo.CurrentCulture) ?? "—"} ms\n" +
            $"Tokens in/out: {decision.InputTokens?.ToString(CultureInfo.CurrentCulture) ?? "—"}/{decision.OutputTokens?.ToString(CultureInfo.CurrentCulture) ?? "—"}";
        if (inhabitant.Proficiency is not null)
            inhabitantSocialDetails.TooltipText += "\nPractice: each completed project earns one point in its domain, up to 30. Every 10 points adds one work per preparation step. Materials, permissions and crop growth time are unchanged.";
        selectedInhabitantCard.Show();
        PositionSelectedInhabitantCard(snapshot);
    }

    private void RenderWorldDetails(OwnerWorldSnapshot snapshot)
    {
        worldDetails.Clear();
        var authoring = snapshot.Authoring;
        var instructions = snapshot.Instructions.Count == 0
            ? "none"
            : string.Join("\n", snapshot.Instructions.Select(instruction =>
                $"#{instruction.SubmissionSequence} {instruction.Kind} → {instruction.TargetInhabitantId}: {instruction.Text} [{instruction.State}]"));
        var cognition = snapshot.Cognition is null
            ? "not reported"
            : $"{snapshot.Cognition.Provider} · " +
              $"{snapshot.Cognition.CurrentCandidateId ?? "no current intention"}";
        var content = snapshot.ContentPackages.Count == 0
            ? "none"
            : string.Join(", ", snapshot.ContentPackages.Select(package =>
                $"{package.PackageId} {package.Version} [{Pretty(package.Lifecycle)}]"));
        var systems = snapshot.WorldSystems is not { } worldSystems
            ? "not reported"
            : $"{Pretty(worldSystems.Season)} / {Pretty(WeatherAtCamera(snapshot))} at camera · " +
              $"{worldSystems.EcologyResourceCount} ecology · {worldSystems.FactionCount} factions · " +
              $"{worldSystems.CurrencyAccountCount} wallets · {worldSystems.CultureCount} cultures · " +
              $"{worldSystems.ChunkCount} chunks · {worldSystems.BuildingDefinitionCount} buildings · " +
              $"{worldSystems.RecipeDefinitionCount} recipes";
        if (authoring is null)
        {
            worldDetails.AppendText($"tick {snapshot.WorldTick}\nworld {snapshot.WorldId}\nNo authoring projection returned.");
            return;
        }

        var stores = snapshot.Stockpiles.Count == 0 ? "No shared stores" : string.Join("\n", snapshot.Stockpiles.Select(stockpile =>
            $"{stockpile.Name}: " + (stockpile.Items.Count == 0 ? "empty" : string.Join(" · ", stockpile.Items.Select(item => $"{Pretty(item.Kind)} {item.Quantity}")))));
        var projects = snapshot.Inhabitants.Where(person => person.Project is not null).Select(person =>
            $"{person.DisplayName}: {person.Project!.Label} · {Pretty(person.Project.Stage)}" +
            (person.Project.Blocker is null ? "" : $"\n  {person.Project.Blocker}"));
        worldDetails.Text = $"{(authoring.IsPaused ? "Paused" : "Playing")} · {DisplayWorldClock(snapshot.WorldTick)}\n" +
            $"{Pretty(authoring.Season)} · {Pretty(WeatherAtCamera(snapshot))} at camera\n\nShared stores\n{stores}\n\nProjects\n{string.Join("\n", projects)}\n\nSocial activity\n" +
            string.Join("\n", snapshot.Inhabitants.SelectMany(person => person.SocialNotes.Take(2).Select(note => $"{person.DisplayName}: {note}")));
        if (snapshot.Council is { } council)
        {
            worldDetails.Text += $"\n\nHousehold council\nSteward: {council.StewardName ?? "awaiting a contributor"}\n" +
                (council.FoodPolicy == "essential_first" ? "Food reserve: hungry members first" : "Shared food: open access") +
                (council.ProposedPolicy is null ? "" : $"\nVote: {Pretty(council.ProposedPolicy)} · {council.Approvals} yes / {council.Rejections} no / {council.Voters} voters");
        }
        worldDetails.TooltipText =
            $"tick {snapshot.WorldTick} · revision {authoring.Revision} · epoch {authoring.RunEpoch}\n" +
            $"state: {(authoring.IsPaused ? "PAUSED — authoring allowed" : "RUNNING — authoring disabled")}\n" +
            $"weather/season: {authoring.Weather} / {authoring.Season}\n" +
            $"current topology: {authoring.CurrentMapManifestDigest}\n" +
            $"initial fixture topology: {authoring.InitialMapManifestDigest}\n" +
            $"cognition: {cognition}\n" +
            $"richer systems: {systems}\n" +
            $"content packages: {content}\n" +
            $"approved assets: {(authoring.ApprovedAssetReferences.Count == 0 ? "none" : string.Join(", ", authoring.ApprovedAssetReferences))}\n\n" +
            $"queued instructions:\n{instructions}";
    }

    private void RenderEventLog()
    {
        eventLog.Clear();
        var snapshot = observationSession.Current?.Baseline.Snapshot;
        var events = knownEvents.Values
            .Where(worldEvent => GameUiText.IsPlayerFacingEvent(worldEvent.Kind))
            .OrderByDescending(worldEvent => worldEvent.EventId)
            .Take(30)
            .ToArray();
        if (events.Length == 0)
        {
            eventLog.AppendText("Nothing notable has happened yet.");
            return;
        }

        foreach (var worldEvent in events)
        {
            var line = $"{DisplayWorldClock(worldEvent.WorldTick)}\n" +
                $"{DescribeWorldEvent(worldEvent, snapshot)}";
            if (worldEvent.Position is not null)
            {
                eventLog.PushMeta(worldEvent.EventId.ToString(CultureInfo.InvariantCulture));
                eventLog.AddText(line + " ↗");
                eventLog.Pop();
            }
            else eventLog.AddText(line);
            eventLog.AddText("\n\n");
        }
    }

    private void JumpToEvent(string eventId)
    {
        if (!long.TryParse(eventId, CultureInfo.InvariantCulture, out var id) ||
            !knownEvents.TryGetValue(id, out var worldEvent) ||
            worldEvent.Position is not { } position)
            return;
        CenterCameraAt(new Vector2(position.X + 0.5f, position.Y + 0.5f));
        eventsPanel.Hide();
    }

    private void SelectInhabitantFromList(long index)
    {
        if (index < 0 || index >= inhabitantList.ItemCount)
        {
            return;
        }

        var inhabitantId = inhabitantList.GetItemMetadata((int)index).AsString();
        if (string.Equals(inhabitantId, selectedInhabitantId, StringComparison.Ordinal))
        {
            ClearInhabitantSelection();
            return;
        }

        selectedInhabitantId = inhabitantId;
        rosterPanel.Hide();
        if (observationSession.Current is { } current)
        {
            RenderInhabitantDetails(current.Baseline.Snapshot);
            RenderSelectedInhabitantCard(current.Baseline.Snapshot);
            RenderMap(current.Baseline.Snapshot);
            RefreshControlAvailability();
        }
    }

    private void SelectInhabitant(string inhabitantId)
    {
        if (string.Equals(inhabitantId, selectedInhabitantId, StringComparison.Ordinal))
        {
            ClearInhabitantSelection();
            return;
        }

        selectedInhabitantId = inhabitantId;
        for (var index = 0; index < inhabitantList.ItemCount; index++)
        {
            if (string.Equals(inhabitantList.GetItemMetadata(index).AsString(), inhabitantId, StringComparison.Ordinal))
            {
                inhabitantList.Select(index);
                break;
            }
        }

        if (observationSession.Current is { } current)
        {
            RenderInhabitantDetails(current.Baseline.Snapshot);
            RenderSelectedInhabitantCard(current.Baseline.Snapshot);
            RenderMap(current.Baseline.Snapshot);
            RefreshControlAvailability();
        }
    }

    private void ClearInhabitantSelection()
    {
        CloseAgentModelEditor();
        familyTreePanel.Hide();
        memoriesPanel.Hide();
        selectedInhabitantId = null;
        inhabitantList.DeselectAll();
        if (observationSession.Current is { } current)
        {
            RenderInhabitantDetails(current.Baseline.Snapshot);
            RenderSelectedInhabitantCard(current.Baseline.Snapshot);
            RenderMap(current.Baseline.Snapshot);
            RefreshControlAvailability();
        }
    }

    private void RefreshControlAvailability()
    {
        var paired = !registeredEndpointInvalid && registration is not null && deviceKey is not null;
        worldSettingsCategoryButton.Disabled = !paired || !isInWorld || returnToMainMenu ||
            worldSettingsContent.Visible;
        var snapshot = observationSession.Current?.Baseline.Snapshot;
        var paused = snapshot?.Authoring?.IsPaused == true;
        var selected = snapshot?.Inhabitants.FirstOrDefault(item =>
            string.Equals(item.Id, selectedInhabitantId, StringComparison.Ordinal));
        var actionDisabled = !paired || isOwnerAction || pendingSubmission is not null;
        autosaveApplyButton.Disabled = actionDisabled || !paused || !autosaveSettingsLoaded;
        var supportsLifePace = snapshot?.LifePaceRate is not null;
        var supportsJevAssistance = snapshot?.JevEnabled is not null &&
            observationSession.Current?.Handshake.ServerCapabilities.Contains("owner-jev-assistance.v1", StringComparer.Ordinal) == true;
        jevAssistanceToggle.Disabled = actionDisabled || !paused || !supportsJevAssistance;
        applyLifePaceButton.Disabled = actionDisabled || !paused || !supportsLifePace;
        lifePaceChoice.Disabled = actionDisabled || !paused || !supportsLifePace;
        applyLifePaceButton.TooltipText = !supportsLifePace ? "This host does not support life pacing." :
            !paused ? "Pause the world before changing life pace." : "Apply future aging speed; existing ages are preserved.";
        if (snapshot?.LifePaceRate is { } rate && (lastObservedLifePace != rate || lastLifePaceWorldId != snapshot.WorldId))
        {
            lifePaceChoice.Select(lifePaceChoice.GetItemIndex(rate));
            lastObservedLifePace = rate;
            lastLifePaceWorldId = snapshot.WorldId;
        }
        worldUrlInput.Editable = registration is null && pendingPairing is null && !isPairingOperation && !isOwnerAction && !isRefreshing;
        connectButton.Disabled = registeredEndpointInvalid || pendingPairing is not null || isPairingOperation || isOwnerAction || isRefreshing;
        pairAgainButton.Visible = registration is not null;
        pairAgainButton.Disabled = isPairingOperation || isOwnerAction || isRefreshing;
        pauseButton.Disabled = actionDisabled || snapshot is null;
        pauseButton.Visible = snapshot?.FounderSetup is not { Started: false };
        founderSetupButton.Disabled = actionDisabled || snapshot?.FounderSetup is not { Started: false };
        addAgentButton.Disabled = actionDisabled || snapshot?.FounderSetup is not { Started: true };
        renameAgentButton.Disabled = actionDisabled || selected is null || selected.IsDraft;
        renameAgentInput.Editable = !actionDisabled && selected is { IsDraft: false };
        startWorldButton.Disabled = actionDisabled || snapshot?.FounderSetup is not { Started: false, Placed: 4 };
        founderProviderChoice.Disabled = actionDisabled;
        founderCredentialChoice.Disabled = actionDisabled;
        founderModelInput.Editable = !actionDisabled;
        founderApiKeyInput.Editable = !actionDisabled;
        founderKeyLabelInput.Editable = !actionDisabled;
        var infantSelected = selected?.DecisionFactors.Any(factor => factor.Key == "age-band" && factor.Detail == "infant") == true;
        var deceasedSelected = selected?.Lifecycle == "dead";
        submitInstructionButton.Disabled = actionDisabled || selected is null || selected.IsDraft || infantSelected || deceasedSelected;
        submitInstructionButton.TooltipText = deceasedSelected ? "Historical profiles cannot receive instructions." :
            infantSelected ? "Direct care through an adult caregiver." : "Send an instruction to this inhabitant.";
        submitAuthoringButton.Disabled = actionDisabled || !paused;
        authoringKind.Disabled = actionDisabled || !paused;
        authoringId.Editable = !actionDisabled && paused;
        authoringValue.Editable = !actionDisabled && paused;
        authoringSecondaryValue.Editable = !actionDisabled && paused;
        authoringX.Editable = !actionDisabled && paused;
        authoringY.Editable = !actionDisabled && paused;
        authoringRenewable.Disabled = actionDisabled || !paused;
        instructionKind.Disabled = actionDisabled || deceasedSelected;
        instructionText.Editable = !actionDisabled && !deceasedSelected;
        retryPendingSubmissionButton.Disabled = !paired || isOwnerAction || pendingSubmission is null;
        forgetPendingSubmissionButton.Disabled = isPairingOperation || isOwnerAction || isRefreshing;
        pairingApprovalId.Editable = !actionDisabled;
        pairingApprovalCode.Editable = !actionDisabled;
        approvePairingButton.Disabled = actionDisabled;
        refreshDevicesButton.Disabled = actionDisabled;
        revokeDeviceId.Editable = !actionDisabled;
        revokeDeviceButton.Disabled = actionDisabled;
        cognitionRoleChoice.Disabled = actionDisabled;
        cognitionProviderChoice.Disabled = actionDisabled;
        cognitionCredentialChoice.Disabled = actionDisabled;
        cognitionModelInput.Editable = !actionDisabled && SelectedProviderId() != "deterministic";
        cognitionApiKeyInput.Editable = !actionDisabled && SelectedProviderId() != "deterministic";
        cognitionCredentialLabelInput.Editable = !actionDisabled;
        refreshCognitionProviderButton.Disabled = actionDisabled;
        applyUsageLimitButton.Disabled = actionDisabled;
        grantUsageCallsButton.Disabled = actionDisabled || usageStatus?.LimitReached != true;
        refreshUsageButton.Disabled = actionDisabled;
        usageAttemptLimitInput.Editable = !actionDisabled;
        var selectedProvider = SelectedProviderId();
        var selectedProviderStatus = providerConfiguration?.Providers.FirstOrDefault(item =>
            string.Equals(item.Provider, selectedProvider, StringComparison.Ordinal));
        saveCognitionProviderButton.Disabled = actionDisabled ||
            SelectedCognitionTarget() is not null && selectedProvider == "jev" ||
            SelectedCognitionTarget() is not null && selectedProviderStatus?.HasCredential != true &&
                selectedProvider is ("openai" or "ollama-cloud") && SelectedCredentialChoice() == "default";
        forgetCognitionCredentialButton.Disabled = actionDisabled || selectedProvider == "deterministic" ||
            selectedProviderStatus?.HasCredential != true;
        deleteCognitionCredentialSlotButton.Disabled = actionDisabled ||
            providerConfiguration?.Assignments?.Any(item => item.CredentialSlotId == SelectedCredentialChoice()) == true;
        // A public key can have only one pending server pairing. Keep the
        // visible comparison value stable until it expires or activates.
        pairButton.Disabled = isPairingOperation || deviceKey is null || pendingPairing is not null || registration is not null;
        forgetRegistrationButton.Disabled = isPairingOperation || registration is null;
    }

    private string SelectedAuthoringKind() => authoringKind.GetItemText(authoringKind.Selected);

    private static string? EmptyToNull(string value)
    {
        var trimmed = value.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string Positions(IReadOnlyList<OwnerWorldPosition> positions) => positions.Count == 0
        ? "none"
        : string.Join(", ", positions.Select(position => $"{position.X},{position.Y}"));

    private static void ConfigureTextPanel(RichTextLabel label, float minimumHeight)
    {
        label.BbcodeEnabled = false;
        label.FitContent = false;
        label.CustomMinimumSize = new Vector2(0, minimumHeight);
        label.ScrollActive = true;
    }

    private void ApplyResponsiveLayout()
    {
        var viewport = mapCanvas.Size;
        if (viewport.X <= 0 || viewport.Y <= 0)
        {
            return;
        }

        climateLabel.Visible = Size.X >= 1100;

        if (observationSession.Current?.Baseline.Snapshot is { } snapshot && HasMap(snapshot))
        {
            var previousTileSize = currentTileSize;
            UpdateMapGeometry(snapshot);
            if (previousTileSize != currentTileSize)
            {
                RenderMap(snapshot);
            }
            else
            {
                PositionSelectedInhabitantCard(snapshot);
            }
        }

        rosterPanel.Position = new Vector2(14, 14);
        settlementPanel.Position = new Vector2(14, 14);
        worldInfoPanel.Position = new Vector2(14, 14);
        selectedTilePanel.Position = new Vector2(14,
            Math.Max(14, viewport.Y - Math.Max(selectedTilePanel.Size.Y,
                selectedTilePanel.CustomMinimumSize.Y) - 14));
        eventsPanel.Position = new Vector2(
            Math.Max(14, viewport.X - Math.Max(eventsPanel.Size.X, eventsPanel.CustomMinimumSize.X) - 14),
            14);
        var familySize = new Vector2(Math.Clamp(viewport.X - 28, 320, 840),
            Math.Clamp(viewport.Y - 28, 280, 600));
        familyTreePanel.Size = familySize;
        familyTreePanel.Position = new Vector2(
            Math.Max(14, (viewport.X - familySize.X) / 2),
            Math.Max(14, (viewport.Y - familySize.Y) / 2));
        var memoriesSize = new Vector2(Math.Clamp(viewport.X - 28, 320, 600),
            Math.Clamp(viewport.Y - 28, 280, 430));
        memoriesPanel.Size = memoriesSize;
        memoriesPanel.Position = new Vector2(
            Math.Max(14, (viewport.X - memoriesSize.X) / 2),
            Math.Max(14, (viewport.Y - memoriesSize.Y) / 2));

        var menuWidth = Math.Min(560, Math.Max(320, viewport.X - 28));
        gameMenuPanel.CustomMinimumSize = new Vector2(menuWidth, 0);

        var toastSize = statusToast.GetCombinedMinimumSize();
        statusToast.Position = new Vector2(
            Math.Max(14, (viewport.X - toastSize.X) / 2),
            Math.Max(14, viewport.Y - toastSize.Y - 18));
    }

    private void UpdateMapGeometry(OwnerWorldSnapshot snapshot)
    {
        if (!HasMap(snapshot) || mapCanvas.Size.X <= 0 || mapCanvas.Size.Y <= 0)
        {
            return;
        }

        var (mapWidth, mapHeight) = MapDimensions(snapshot);
        var availableWidth = Math.Max(1, mapCanvas.Size.X - 36 - ((mapWidth - 1) * TileGap));
        var availableHeight = Math.Max(1, mapCanvas.Size.Y - 36 - ((mapHeight - 1) * TileGap));
        var fittedTileSize = (int)Math.Floor(Math.Min(availableWidth / mapWidth, availableHeight / mapHeight));
        var baseTileSize = Math.Clamp(fittedTileSize, 12, 220);
        currentTileSize = Math.Clamp((int)MathF.Round(baseTileSize * cameraZoom), 8, 880);

        var stageSize = new Vector2(
            (mapWidth * currentTileSize) + ((mapWidth - 1) * TileGap),
            (mapHeight * currentTileSize) + ((mapHeight - 1) * TileGap));
        mapStage.Size = stageSize;
        terrainLayer.Size = stageSize;
        var stride = currentTileSize + TileGap;
        if (snapshot.WrapsEastWest)
            cameraCenterTiles.X = PositiveMod(cameraCenterTiles.X, mapWidth);
        mapStage.Position = new Vector2(
            snapshot.WrapsEastWest
                ? mapCanvas.Size.X / 2 - cameraCenterTiles.X * stride
                : CameraAxis(cameraCenterTiles.X, stageSize.X, mapCanvas.Size.X, stride),
            CameraAxis(cameraCenterTiles.Y, stageSize.Y, mapCanvas.Size.Y, stride));
        cameraCenterTiles = new Vector2(
            snapshot.WrapsEastWest ? cameraCenterTiles.X : (mapCanvas.Size.X / 2 - mapStage.Position.X) / stride,
            (mapCanvas.Size.Y / 2 - mapStage.Position.Y) / stride);
        RepositionWrappedMapMarkers(mapWidth, stride, snapshot.WrapsEastWest);
        RefreshOverviewViewport(mapWidth, mapHeight, stride, snapshot.WrapsEastWest);
        RefreshTileHoverAtMouse();
        RenderWorldHud(snapshot);
        RenderWorldInfo(snapshot);
    }

    private static float CameraAxis(float centerTile, float stagePixels, float viewportPixels, float stride) =>
        stagePixels <= viewportPixels
            ? (viewportPixels - stagePixels) / 2
            : Math.Clamp((viewportPixels / 2) - (centerTile * stride), viewportPixels - stagePixels, 0);

    private static float PositiveMod(float value, int modulus) => (value % modulus + modulus) % modulus;

    private float WrappedMarkerX(float canonicalX, int mapWidth, float stride, bool wrapsEastWest) =>
        !wrapsEastWest ? canonicalX :
        canonicalX + MathF.Round((cameraCenterTiles.X - canonicalX / stride) / mapWidth) * mapWidth * stride;

    private void RepositionWrappedMapMarkers(int mapWidth, float stride, bool wrapsEastWest)
    {
        foreach (var (id, visual) in mapObjectVisuals)
            if (mapObjectCanonicalXs.TryGetValue(id, out var x))
                visual.Position = new Vector2(WrappedMarkerX(x, mapWidth, stride, wrapsEastWest), visual.Position.Y);
        foreach (var (id, visual) in inhabitantVisuals)
            if (inhabitantCanonicalXs.TryGetValue(id, out var x))
                visual.Position = new Vector2(WrappedMarkerX(x, mapWidth, stride, wrapsEastWest), visual.Position.Y);
    }

    private void RefreshOverviewViewport(int mapWidth, int mapHeight, float stride, bool wrapsEastWest)
    {
        var left = wrapsEastWest ? -mapStage.Position.X / stride :
            Math.Clamp(-mapStage.Position.X / stride, 0, mapWidth);
        var top = Math.Clamp(-mapStage.Position.Y / stride, 0, mapHeight);
        var right = wrapsEastWest ? (mapCanvas.Size.X - mapStage.Position.X) / stride :
            Math.Clamp((mapCanvas.Size.X - mapStage.Position.X) / stride, 0, mapWidth);
        var bottom = Math.Clamp((mapCanvas.Size.Y - mapStage.Position.Y) / stride, 0, mapHeight);
        var visible = new Rect2(left, top, right - left, bottom - top);
        worldOverview.SetVisibleTiles(visible);
        terrainLayer.SetCamera(visible, currentTileSize, TileGap, wrapsEastWest);
    }

    private void CenterCameraAt(Vector2 tileCenter)
    {
        if (renderedMapSnapshot is not { } snapshot || !HasMap(snapshot))
        {
            return;
        }

        cameraCenterTiles = tileCenter;
        UpdateMapGeometry(snapshot);
        PositionSelectedInhabitantCard(snapshot);
    }

    private void PanCamera(Vector2 deltaTiles)
    {
        if (renderedMapSnapshot is not { } snapshot || !HasMap(snapshot))
        {
            return;
        }

        CenterCameraAt(cameraCenterTiles + deltaTiles);
    }

    private void HandleMapInput(InputEvent @event)
    {
        if (gameMenuPanel.Visible ||
            renderedMapSnapshot is not { } snapshot || !HasMap(snapshot))
        {
            return;
        }

        if (@event is InputEventMouseButton mouse)
        {
            if (mouse.Pressed && mouse.ButtonIndex == MouseButton.Left && founderSetupPanel.Visible &&
                snapshot.FounderSetup is { Started: false })
            {
                _ = PlaceFounderAtAsync(TileAtCanvas(mouse.Position, snapshot));
                mapCanvas.AcceptEvent();
            }
            else if (mouse.Pressed && mouse.ButtonIndex == MouseButton.Left && founderSetupPanel.Visible &&
                placingAddedAgent && snapshot.FounderSetup is { Started: true })
            {
                _ = PlaceAgentAtAsync(TileAtCanvas(mouse.Position, snapshot));
                mapCanvas.AcceptEvent();
            }
            else if (mouse.ButtonIndex == MouseButton.Middle)
            {
                draggingMap = mouse.Pressed;
                mapCanvas.AcceptEvent();
            }
            else if (mouse.Pressed && mouse.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            {
                var nextZoom = Math.Clamp(cameraZoom * (mouse.ButtonIndex == MouseButton.WheelUp ? 1.25f : 0.8f), 0.65f, 4);
                if (Math.Abs(nextZoom - cameraZoom) > 0.001f)
                {
                    cameraZoom = nextZoom;
                    RenderMap(snapshot);
                    PositionSelectedInhabitantCard(snapshot);
                }
                mapCanvas.AcceptEvent();
            }
            else if (mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
            {
                var tile = TileAtCanvas(mouse.Position, snapshot);
                if (MapContains(snapshot, tile.X, tile.Y))
                {
                    selectedTile = tile;
                    terrainLayer.SetSelectedTile(tile);
                    RenderTileInspection(snapshot);
                    selectedTilePanel.Show();
                    mapCanvas.AcceptEvent();
                }
            }
        }
        else if (@event is InputEventMouseMotion hoverMotion)
        {
            if (draggingMap)
            {
                PanCamera(-hoverMotion.Relative / (currentTileSize + TileGap));
                mapCanvas.AcceptEvent();
            }
            UpdateTileHover(hoverMotion.Position);
        }
    }

    private void RefreshTileHoverAtMouse() => UpdateTileHover(mapCanvas.GetLocalMousePosition());

    private Vector2I TileAtCanvas(Vector2 canvasPosition, OwnerWorldSnapshot snapshot)
    {
        var tile = (canvasPosition - mapStage.Position) / (currentTileSize + TileGap);
        var x = Mathf.FloorToInt(tile.X);
        if (snapshot.WrapsEastWest)
            x = ((x % terrainMap!.Width) + terrainMap.Width) % terrainMap.Width;
        return new Vector2I(x, Mathf.FloorToInt(tile.Y));
    }

    private void ClearTileSelection()
    {
        selectedTile = null;
        terrainLayer.SetSelectedTile(null);
        selectedTilePanel.Hide();
    }

    private void RenderTileInspection(OwnerWorldSnapshot snapshot)
    {
        if (selectedTile is not { } tile || terrainMap is null) return;
        if (!MapContains(snapshot, tile.X, tile.Y))
        {
            ClearTileSelection();
            return;
        }

        var regionSize = Math.Max(1, snapshot.WeatherRegionSize);
        var region = snapshot.WeatherRegions.FirstOrDefault(item =>
            item.X == tile.X / regionSize && item.Y == tile.Y / regionSize);
        var objects = snapshot.Objects.Where(item => item.Position.X == tile.X && item.Position.Y == tile.Y)
            .Select(item => Pretty(item.Kind))
            .Concat(snapshot.Resources.Where(item => item.Position.X == tile.X && item.Position.Y == tile.Y)
                .Select(item => item.TreeKind is { } tree
                    ? $"{Pretty(tree)} tree · {Pretty(item.TreeStage ?? item.State)}"
                    : $"{Pretty(item.Kind)} site" +
                        (item.Quantity is { } quantity ? $" · {quantity} available" : string.Empty)))
            .Concat(snapshot.PlacedBuildings.Where(item =>
                    tile.X >= item.Position.X && tile.X < item.Position.X + item.Width &&
                    tile.Y >= item.Position.Y && tile.Y < item.Position.Y + item.Height)
                .Select(item => item.DisplayName ?? Pretty(item.DefinitionId)))
            .ToArray();
        var climate = WorldTerrainMap.ClimateName(terrainMap.ClimateAt(tile.X, tile.Y)) ?? "unavailable";
        var elevation = terrainMap.ElevationAt(tile.X, tile.Y);
        var hydrology = WorldTerrainMap.HydrologyName(terrainMap.HydrologyAt(tile.X, tile.Y)) ?? "unavailable";
        var surface = WorldTerrainMap.SurfaceName(terrainMap.SurfaceAt(tile.X, tile.Y)) ?? "unavailable";
        var vegetation = WorldTerrainMap.VegetationName(terrainMap.VegetationAt(tile.X, tile.Y)) ?? "unavailable";
        selectedTileText.Text =
            $"Tile {tile.X}, {tile.Y}\n" +
            $"Terrain kind: {WorldTerrainMap.NameFor(terrainMap.At(tile.X, tile.Y))}\n" +
            $"Climate: {climate}\n" +
            $"Surface: {surface}\n" +
            $"Hydrology: {hydrology}\n" +
            $"Vegetation: {vegetation}\n" +
            $"Weather: {Pretty(region?.Weather ?? snapshot.Authoring?.Weather ?? "unavailable")}\n" +
            $"Regional soil moisture: {(region?.SoilMoisture is { } moisture ? moisture + "/100" : "unavailable")}\n" +
            $"Elevation: {(elevation is { } level ? level + "/255" : "unavailable")}\n" +
            $"Fertility: unavailable\n" +
            $"Objects: {(objects.Length == 0 ? "none observed" : string.Join(", ", objects))}";
    }

    private void UpdateTileHover(Vector2 canvasPosition)
    {
        if (renderedMapSnapshot is not { } snapshot || !HasMap(snapshot) ||
            gameMenuPanel.Visible ||
            canvasPosition.X < 0 || canvasPosition.Y < 0 ||
            canvasPosition.X >= mapCanvas.Size.X || canvasPosition.Y >= mapCanvas.Size.Y)
        {
            terrainLayer.SetHoveredTile(null);
            return;
        }

        var stagePosition = canvasPosition - mapStage.Position;
        var tile = TileAtCanvas(canvasPosition, snapshot);
        if (!MapContains(snapshot, tile.X, tile.Y) ||
            inhabitantVisuals.Values.Any(marker => marker.Visible &&
                new Rect2(marker.Position, marker.Size).HasPoint(stagePosition)))
        {
            terrainLayer.SetHoveredTile(null);
            return;
        }

        terrainLayer.SetHoveredTile(tile);
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true } key || mainMenuOverlay.Visible || gameMenuPanel.Visible ||
            GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit)
        {
            return;
        }

        var direction = key.Keycode switch
        {
            Key.W or Key.Up => new Vector2(0, -1),
            Key.A or Key.Left => new Vector2(-1, 0),
            Key.S or Key.Down => new Vector2(0, 1),
            Key.D or Key.Right => new Vector2(1, 0),
            _ => Vector2.Zero,
        };
        if (direction != Vector2.Zero)
        {
            PanCamera(direction * 1.5f);
            GetViewport().SetInputAsHandled();
        }
    }

    private void PositionSelectedInhabitantCard(OwnerWorldSnapshot snapshot)
    {
        if (!selectedInhabitantCard.Visible || mapCanvas.Size.X <= 0 || mapCanvas.Size.Y <= 0)
        {
            return;
        }

        var inhabitant = snapshot.Inhabitants.FirstOrDefault(item =>
            string.Equals(item.Id, selectedInhabitantId, StringComparison.Ordinal));
        if (inhabitant is null)
        {
            return;
        }

        var cardWidth = Math.Min(370, Math.Max(300, mapCanvas.Size.X - 24));
        selectedInhabitantCard.CustomMinimumSize = new Vector2(cardWidth, 0);
        var cardSize = selectedInhabitantCard.GetCombinedMinimumSize();
        if (string.Equals(inhabitant.Lifecycle, "dead", StringComparison.OrdinalIgnoreCase))
        {
            selectedInhabitantCard.Position = new Vector2(Math.Max(12, mapCanvas.Size.X - cardWidth - 12), 12);
            return;
        }
        var stride = currentTileSize + TileGap;
        var actorCenter = mapStage.Position + new Vector2(
            (inhabitant.Position.X * stride) + (currentTileSize / 2f),
            (inhabitant.Position.Y * stride) + (currentTileSize / 2f));
        var x = Math.Clamp(actorCenter.X - (cardWidth / 2), 12, Math.Max(12, mapCanvas.Size.X - cardWidth - 12));
        var y = actorCenter.Y - (currentTileSize / 2f) - cardSize.Y - 12;
        if (y < 12)
        {
            y = actorCenter.Y + (currentTileSize / 2f) + 12;
        }

        y = Math.Clamp(y, 12, Math.Max(12, mapCanvas.Size.Y - cardSize.Y - 12));
        selectedInhabitantCard.Position = new Vector2(x, y);
    }

    private static PanelContainer NewPanel(string title, Control content)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", PanelStyle());
        AddPanelContents(panel, title, content);
        return panel;
    }

    private static void AddPanelContents(PanelContainer panel, Control content) => AddPanelContents(panel, string.Empty, content);

    private static void AddPanelContents(PanelContainer panel, string title, Control content)
    {
        panel.AddThemeStyleboxOverride("panel", PanelStyle());
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 7);
        if (!string.IsNullOrWhiteSpace(title))
        {
            var heading = new Label { Text = title };
            heading.AddThemeFontSizeOverride("font_size", 15);
            body.AddChild(heading);
        }

        body.AddChild(content);
        margin.AddChild(body);
        panel.AddChild(margin);
    }

    private static HBoxContainer MetricRow(string caption, Label value)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        var label = new Label
        {
            Text = caption,
            CustomMinimumSize = new Vector2(82, 0),
        };
        label.Modulate = new Color("8FA5A7");
        row.AddChild(label);
        value.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        value.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        row.AddChild(value);
        return row;
    }

    private static void StyleButton(Button button, bool primary = false)
    {
        button.CustomMinimumSize = new Vector2(0, 34);
        button.AddThemeStyleboxOverride("normal", ButtonStyle(
            primary ? new Color("2C706B") : new Color("20343B"),
            primary ? new Color("80CDBA") : new Color("49656A")));
        button.AddThemeStyleboxOverride("hover", ButtonStyle(
            primary ? new Color("38877E") : new Color("2B464D"),
            new Color("B0DFCE")));
        button.AddThemeStyleboxOverride("pressed", ButtonStyle(
            primary ? new Color("225A58") : new Color("182A31"),
            new Color("D8C6A5")));
        button.AddThemeStyleboxOverride("disabled", ButtonStyle(
            new Color("17232A"),
            new Color("2A3A40")));
        button.AddThemeColorOverride("font_color", new Color("E5EFEA"));
        button.AddThemeColorOverride("font_hover_color", new Color("FFFFFF"));
        button.AddThemeColorOverride("font_pressed_color", new Color("FFFFFF"));
        button.AddThemeColorOverride("font_disabled_color", new Color("718486"));
    }

    private static StyleBoxFlat ButtonStyle(Color background, Color border) => new()
    {
        BgColor = background,
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        BorderColor = border,
        CornerRadiusTopLeft = 6,
        CornerRadiusTopRight = 6,
        CornerRadiusBottomLeft = 6,
        CornerRadiusBottomRight = 6,
        ContentMarginLeft = 12,
        ContentMarginRight = 12,
        ContentMarginTop = 7,
        ContentMarginBottom = 7,
    };

    private static StyleBoxFlat InnerPanelStyle() => new()
    {
        BgColor = new Color("111D24"),
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        BorderColor = new Color("263D44"),
        CornerRadiusTopLeft = 6,
        CornerRadiusTopRight = 6,
        CornerRadiusBottomLeft = 6,
        CornerRadiusBottomRight = 6,
    };

    private static StyleBoxFlat PanelStyle() => new()
    {
        BgColor = new Color("192631"),
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        BorderColor = new Color("345363"),
        CornerRadiusTopLeft = 10,
        CornerRadiusTopRight = 10,
        CornerRadiusBottomLeft = 10,
        CornerRadiusBottomRight = 10,
    };

    private static StyleBoxFlat TopBarStyle() => new()
    {
        BgColor = new Color("162127"),
        BorderWidthBottom = 1,
        BorderColor = new Color("314A4A"),
        ContentMarginLeft = 0,
        ContentMarginRight = 0,
        ContentMarginTop = 0,
        ContentMarginBottom = 0,
    };

    private Uri ResolveWorldUri()
    {
        if (!WorldServerOrigin.TryResolve(worldUrlInput.Text, out var configuredWorldUri))
        {
            throw new InvalidOperationException("World URL must be an absolute HTTPS origin (or loopback HTTP for local development).");
        }

        if (registration is null)
        {
            if (pendingPairingOrigin is not null)
            {
                if (!WorldServerOrigin.Same(configuredWorldUri, pendingPairingOrigin))
                {
                    throw new InvalidOperationException("This pending pairing is pinned to the server that created it. Wait for it to expire or forget the local registration before changing servers.");
                }

                return pendingPairingOrigin;
            }

            return configuredWorldUri;
        }

        if (!WorldServerOrigin.TryResolve(registration.WorldUrl, out var pinnedWorldUri) ||
            !WorldServerOrigin.Same(configuredWorldUri, pinnedWorldUri))
        {
            throw new InvalidOperationException("This paired device is pinned to its original server origin. Forget the local registration before pairing it with a different server.");
        }

        return pinnedWorldUri;
    }

    private static string ConfiguredWorldUrl() => ProjectSettings
        .GetSetting("clankerworld/world_url", "http://127.0.0.1:5188")
        .AsString();

    private static bool TryGetCommandLineWorldUrl(out string? worldUrl)
    {
        var argument = OS.GetCmdlineUserArgs()
            .FirstOrDefault(value => value.StartsWith("--world-url=", StringComparison.Ordinal));
        worldUrl = argument is null ? null : argument["--world-url=".Length..];
        return !string.IsNullOrWhiteSpace(worldUrl);
    }

    private void SetStatus(string text, bool good)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            statusToast.Hide();
            return;
        }

        statusLabel.Text = text;
        statusLabel.Modulate = new Color(good ? "B9E8C5" : "F0B6A6");
        statusToast.Show();
        ApplyResponsiveLayout();
    }

    private void ShowHeldState(string reason)
    {
        var heldTick = observationSession.Current?.Baseline.Snapshot.WorldTick;
        SetStatus(
            heldTick is null
                ? $"disconnected · {reason}"
                : $"disconnected · holding accepted tick {heldTick} · {reason}",
            good: false);
    }

    private static string FriendlyFailure(Exception exception) => exception switch
    {
        System.Net.Http.HttpRequestException requestException when requestException.StatusCode is not null =>
            $"HTTP {(int)requestException.StatusCode.Value} {requestException.StatusCode.Value}",
        _ => exception.Message,
    };

    private static string DescribeWorldEvent(OwnerWorldEvent worldEvent, OwnerWorldSnapshot? snapshot)
    {
        var parts = worldEvent.Detail.Split(':', StringSplitOptions.RemoveEmptyEntries);
        string NameAt(int index)
        {
            if (index >= parts.Length)
            {
                return "Someone";
            }

            return snapshot?.Inhabitants.FirstOrDefault(inhabitant => inhabitant.Id == parts[index])?.DisplayName
                ?? GameUiText.HumanizeIdentifier(parts[index]);
        }

        string ThingAt(int index) => index < parts.Length
            ? GameUiText.HumanizeIdentifier(parts[index])
            : "something new";

        return worldEvent.Kind switch
        {
            "world_created" => "A new world has begun.",
            "weather_changed" when parts.Length >= 2 => $"The weather changed to {ThingAt(1)}.",
            "building_placed" => $"{ThingAt(1)} was built.",
            "build_started" => $"Work began on {ThingAt(1)}.",
            "build_completed" => $"{ThingAt(1)} is ready.",
            "recipe_started" => $"Work began on {ThingAt(1)}.",
            "recipe_completed" => $"{ThingAt(1)} was finished.",
            "crop_moisture_effect" when parts.Length >= 3 => parts[1] == "wet"
                ? "Moist soil improved a crop harvest."
                : "Dry soil reduced a crop harvest.",
            "food_harvested" => $"{NameAt(0)} gathered food.",
            "food_consumed" => $"{NameAt(0)} ate.",
            "inhabitant_slept" => $"{NameAt(0)} slept.",
            "child_born" => $"{NameAt(0)} was born.",
            "inhabitant_removed" => $"{NameAt(0)} died.",
            "estate_will_accepted" => "A final will directed a personal estate.",
            "estate_will_default" => "A personal estate followed household inheritance.",
            "inhabitant_building_proposed" => $"{NameAt(0)} proposed a new building design.",
            "settlement_founded" => "A new settlement was founded.",
            "paused" => "The world was paused.",
            "resumed" => "The world resumed.",
            _ => $"{GameUiText.HumanizeIdentifier(worldEvent.Kind)}.",
        };
    }

    private static string PositionKey(OwnerWorldPosition position) => $"{position.X},{position.Y}";

    private static string ActorLabel(string displayName)
    {
        var trimmed = displayName.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return "?";
        }

        return trimmed.Length <= 8 ? trimmed : $"{trimmed[..7]}…";
    }

    private static string ActivityGlyph(string? candidateId) => candidateId switch
    {
        "seek_food" => "→",
        "harvest_food" => "✦",
        "consume_food" => "♥",
        not null when candidateId.StartsWith("build:", StringComparison.Ordinal) => "◆",
        "safe_idle" => "·",
        _ => "○",
    };

    private static string ResourceMarker(string kind) => kind switch
    {
        "food" => "FOOD",
        "construction" => "WOOD",
        "stone" => "STONE",
        "fiber" => "FIBER",
        "seed" => "SEEDS",
        _ => ShortMarker(kind),
    };

    private static string ResourceGlyph(string kind) => kind switch
    {
        "food" => "●",
        "construction" => "▰",
        "stone" => "⬟",
        "fiber" => "♧",
        "seed" => "✦",
        _ => "◆",
    };

    private static string ObjectMarker(string kind) => kind switch
    {
        "campfire" => "FIRE",
        "shelter" => "HOME",
        "tree" => "TREE",
        _ => ShortMarker(kind),
    };

    private static string ObjectGlyph(string kind) => kind switch
    {
        "campfire" => "✦",
        "shelter" => "⌂",
        "tree" => "♣",
        _ => "■",
    };

    private static string ShortMarker(string value)
    {
        var compact = value.Trim().Replace('_', ' ');
        return compact.Length <= 6 ? compact.ToUpperInvariant() : $"{compact[..5].ToUpperInvariant()}…";
    }

    private static string Pretty(string value) => string.IsNullOrWhiteSpace(value)
        ? "unknown"
        : string.Join(' ', value.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Length == 1
                ? part.ToUpperInvariant()
                : char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant()));

    private static int NeedPercent(int basisPoints) => Math.Clamp(basisPoints / 100, 0, 100);

}
