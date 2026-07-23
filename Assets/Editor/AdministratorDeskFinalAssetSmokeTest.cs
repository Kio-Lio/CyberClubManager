using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class AdministratorDeskFinalAssetSmokeTest
{
    private const string AssetPath =
        "Assets/Resources/Environment/Reception/AdministratorDesk_Final.png";
    private const string PendingKey =
        "CyberClub.AdministratorDeskFinalAsset.Pending";
    private const string FailedKey =
        "CyberClub.AdministratorDeskFinalAsset.Failed";
    private const string PrimaryFingerprintKey =
        "CyberClub.AdministratorDeskFinalAsset.PrimaryFingerprint";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    private static readonly string QASavePath = SaveStorageProfile.QASavePath;
    private static readonly string QASaveBackupPath = Path.Combine(
        Path.GetTempPath(),
        "cyber_club_administrator_desk_qa_save.bak"
    );
    private static readonly List<string> RuntimeErrors = new();

    private static double verifyAt;

    static AdministratorDeskFinalAssetSmokeTest()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        Application.logMessageReceived -= OnLogMessageReceived;
        Application.logMessageReceived += OnLogMessageReceived;
    }

    public static void Run()
    {
        try
        {
            ValidateAssetImport();
            SaveStorageProfile.UseQASandbox();
            BackupQASave();
            CapturePrimaryFingerprint();
            RuntimeErrors.Clear();

            if (File.Exists(QASavePath))
            {
                File.Delete(QASavePath);
            }

            EditorPrefs.SetBool(PendingKey, true);
            EditorPrefs.SetBool(FailedKey, false);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (!EditorPrefs.GetBool(PendingKey, false))
        {
            return;
        }

        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            SaveStorageProfile.UseQASandbox();
            verifyAt = EditorApplication.timeSinceStartup + 4d;
            return;
        }

        if (state != PlayModeStateChange.EnteredEditMode)
        {
            return;
        }

        bool failed = EditorPrefs.GetBool(FailedKey, false);
        try
        {
            ValidatePrimaryFingerprint();
        }
        catch (Exception exception)
        {
            failed = true;
            Debug.LogError(
                $"ADMINISTRATOR_DESK_FINAL_ASSET_SMOKE_TEST: FAIL\n{exception}"
            );
        }
        finally
        {
            RestoreQASave();
            EditorPrefs.DeleteKey(PendingKey);
            EditorPrefs.DeleteKey(FailedKey);
            EditorPrefs.DeleteKey(PrimaryFingerprintKey);
        }

        if (failed)
        {
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log("ADMINISTRATOR_DESK_FINAL_ASSET_SMOKE_TEST: PASS");
        EditorApplication.Exit(0);
    }

    private static void Tick()
    {
        if (!EditorPrefs.GetBool(PendingKey, false) ||
            !EditorApplication.isPlaying ||
            EditorApplication.timeSinceStartup < verifyAt)
        {
            return;
        }

        verifyAt = double.MaxValue;
        try
        {
            VerifyPolishPass();
            EditorApplication.isPlaying = false;
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private static void VerifyPolishPass()
    {
        Require(RuntimeErrors.Count == 0,
            $"Runtime errors were logged: {string.Join(" | ", RuntimeErrors)}");
        Require(SaveStorageProfile.IsQASandboxActive,
            "Editor Play Mode did not activate the QA save sandbox.");
        Require(!string.Equals(
                SaveStorageProfile.ActiveSavePath,
                SaveStorageProfile.PrimarySavePath,
                StringComparison.OrdinalIgnoreCase),
            "QA and primary saves use the same physical path.");
        Require(SaveManager.Instance != null &&
                SaveManager.Instance.TrySaveGame() &&
                File.Exists(QASavePath),
            "The isolated QA save could not be written.");

        ValidateRuntimeHierarchy();
        ValidateScaleAndPlacement();
        ValidateCollidersAndNavigation();
        ValidateSortingAndInteraction();

        Require(RuntimeErrors.Count == 0,
            $"Runtime errors were logged: {string.Join(" | ", RuntimeErrors)}");
    }

    private static void ValidateAssetImport()
    {
        Require(File.Exists(AssetPath),
            $"Final administrator desk PNG is missing: {AssetPath}.");

        TextureImporter importer = AssetImporter.GetAtPath(AssetPath)
            as TextureImporter;
        Require(importer != null,
            "Administrator desk has no TextureImporter.");
        Require(importer.textureType == TextureImporterType.Sprite &&
                importer.spriteImportMode == SpriteImportMode.Single,
            "Administrator desk is not imported as a Single Sprite.");
        Require(importer.alphaIsTransparency && !importer.mipmapEnabled,
            "Administrator desk alpha or mipmap settings are invalid.");
        Require(importer.wrapMode == TextureWrapMode.Clamp &&
                importer.filterMode == FilterMode.Point,
            "Administrator desk filtering must use Point and Clamp.");
        Require(importer.textureCompression ==
                TextureImporterCompression.Uncompressed,
            "Administrator desk must remain uncompressed to avoid alpha halos.");
        TextureImporterSettings importerSettings = new();
        importer.ReadTextureSettings(importerSettings);
        Require(importerSettings.spriteMeshType == SpriteMeshType.Tight &&
                Mathf.Abs(importer.spritePixelsPerUnit - 512f) < 0.01f,
            "Administrator desk mesh type or PPU is invalid.");
        Require(Vector2.Distance(
                    importer.spritePivot,
                    new Vector2(0.5f, 0f)) < 0.001f,
            "Administrator desk pivot is not bottom-center.");

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetPath);
        Require(sprite != null && sprite.name == "AdministratorDesk_Final",
            "Final administrator desk sprite could not be loaded.");
        ValidateAlphaPadding();
    }

    private static void ValidateAlphaPadding()
    {
        string fullPath = Path.GetFullPath(Path.Combine(
            Application.dataPath,
            "..",
            AssetPath
        ));
        Texture2D texture = new(2, 2, TextureFormat.RGBA32, false);
        try
        {
            Require(ImageConversion.LoadImage(
                    texture,
                    File.ReadAllBytes(fullPath),
                    false),
                "Final administrator desk PNG could not be decoded.");

            Color32[] pixels = texture.GetPixels32();
            int minX = texture.width;
            int minY = texture.height;
            int maxX = -1;
            int maxY = -1;
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    if (pixels[y * texture.width + x].a <= 3)
                    {
                        continue;
                    }

                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            Require(maxX >= minX && maxY >= minY,
                "Final administrator desk PNG is fully transparent.");
            Require(minX <= texture.width * 0.05f &&
                    texture.width - 1 - maxX <= texture.width * 0.05f &&
                    minY <= texture.height * 0.05f &&
                    texture.height - 1 - maxY <= texture.height * 0.05f,
                "Final administrator desk contains excessive transparent padding.");

            int topCenter = (texture.height - 16) * texture.width +
                texture.width / 2;
            Require(pixels[0].a == 0 &&
                    pixels[texture.width - 1].a == 0 &&
                    pixels[topCenter].a == 0,
                "Final administrator desk does not preserve real transparent alpha.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private static void ValidateRuntimeHierarchy()
    {
        AdministratorDeskVisualPresenter[] presenters =
            UnityEngine.Object.FindObjectsByType<
                AdministratorDeskVisualPresenter>();
        Require(presenters.Length == 1,
            $"Expected one administrator desk presenter, found {presenters.Length}.");

        AdministratorDeskVisualPresenter presenter = presenters[0];
        GameObject desk = presenter.gameObject;
        Require(desk.name == "AdministratorDesk",
            "Final desk presenter is attached to a duplicate object.");
        Require(UnityEngine.Object.FindObjectsByType<
                    AdministratorDeskInteraction>().Length == 1,
            "Administrator desk interaction marker is missing or duplicated.");

        Transform visualRoot = desk.transform.Find("AdministratorDeskVisual");
        Require(visualRoot != null &&
                CountNamedChildren(desk.transform,
                    "AdministratorDeskVisual") == 1,
            "AdministratorDeskVisual is missing or duplicated.");
        Require(CountNamedChildren(visualRoot, "DeskSprite") == 1 &&
                CountNamedChildren(visualRoot, "DeskShadow") == 1 &&
                CountNamedChildren(visualRoot, "InteractionGlow") == 1,
            "Final desk visual hierarchy contains missing or duplicate parts.");

        SpriteRenderer placeholder = desk.GetComponent<SpriteRenderer>();
        Require(placeholder != null && !placeholder.enabled,
            "The old administrator desk placeholder renderer is still visible.");
        Require(presenter.DeskRenderer != null &&
                presenter.DeskRenderer.enabled &&
                presenter.DeskRenderer.sprite != null &&
                presenter.DeskRenderer.sprite.name ==
                    "AdministratorDesk_Final",
            "The active final administrator desk sprite is invalid.");
        Require(presenter.InteractionGlowRenderer != null &&
                !presenter.InteractionGlowRenderer.enabled,
            "Administrator desk interaction glow is visible while idle.");

        int activeFinalSprites = 0;
        foreach (SpriteRenderer renderer in
                 desk.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer.enabled &&
                renderer.sprite == presenter.DeskRenderer.sprite)
            {
                activeFinalSprites++;
            }
        }
        Require(activeFinalSprites == 1,
            "More than one final administrator desk sprite is active.");
    }

    private static void ValidateScaleAndPlacement()
    {
        AdministratorDeskVisualPresenter presenter =
            RequireObject("AdministratorDesk")
                .GetComponent<AdministratorDeskVisualPresenter>();
        Transform workstationTransform = RequireObject("PC_01")
            .transform.Find("PCVisual/WorkstationSprite");
        SpriteRenderer workstation = workstationTransform != null
            ? workstationTransform.GetComponent<SpriteRenderer>()
            : null;
        Require(presenter != null && presenter.DeskRenderer != null &&
                workstation != null && workstation.sprite != null,
            "Desk or workstation renderer is missing.");

        float ratio = presenter.DeskRenderer.bounds.size.x /
            workstation.bounds.size.x;
        Require(ratio >= 2.5f && ratio <= 3f,
            $"Administrator desk width ratio is {ratio:F2}, expected 2.5-3.0.");
        Require(Vector2.Distance(
                    presenter.transform.position,
                    new Vector2(-4.4f, 2.9f)) < 0.03f &&
                presenter.transform.localScale == Vector3.one,
            "Administrator desk logical transform changed.");

        Bounds receptionBounds =
            RequireRenderer("FloorZone_Reception").bounds;
        Bounds deskBounds = presenter.DeskRenderer.bounds;
        Require(ContainsXY(receptionBounds, deskBounds.min) &&
                ContainsXY(receptionBounds, deskBounds.max),
            "Administrator desk extends outside the reception zone.");

        foreach (string terminalName in new[]
                 {
                     "ClubResearchTerminal",
                     "InternetProviderTerminal"
                 })
        {
            Bounds terminalBounds = GetRendererBounds(
                RequireObject(terminalName)
            );
            Require(!deskBounds.Intersects(terminalBounds),
                $"Administrator desk overlaps {terminalName}.");
        }
    }

    private static void ValidateCollidersAndNavigation()
    {
        GameObject desk = RequireObject("AdministratorDesk");
        BoxCollider2D[] colliders =
            desk.GetComponentsInChildren<BoxCollider2D>(true);
        Require(colliders.Length == 3,
            $"Expected three desk colliders, found {colliders.Length}.");
        foreach (BoxCollider2D collider in colliders)
        {
            Require(!collider.isTrigger,
                $"{collider.name} unexpectedly uses a trigger collider.");
        }
        Require(desk.transform.Find("DeskCollider_Left") != null &&
                desk.transform.Find("DeskCollider_Right") != null,
            "Administrator desk side colliders are missing.");

        Vector2 basePoint = (Vector2)desk.transform.position +
            new Vector2(0f, -0.35f);
        Vector2 openCenter = (Vector2)desk.transform.position +
            new Vector2(0f, 0.32f);
        Vector2 frontPassage = (Vector2)desk.transform.position +
            new Vector2(0f, -1.05f);
        Vector2 staffArea = (Vector2)desk.transform.position +
            new Vector2(0f, 0.70f);
        Require(IsInsideAnyCollider(colliders, basePoint),
            "Administrator desk physical base is not collidable.");
        Require(!IsInsideAnyCollider(colliders, openCenter),
            "Administrator desk collider blocks its transparent U-shaped center.");
        Require(!IsInsideAnyCollider(colliders, frontPassage),
            "Administrator desk blocks the front passage.");
        Require(!IsInsideAnyCollider(colliders, staffArea),
            "Administrator desk blocks the staff area behind it.");

        ClientNavigationManager navigation =
            ClientNavigationManager.Instance;
        Require(navigation != null &&
                navigation.BuildPath(
                    navigation.EntranceNode,
                    navigation.QueueNode).Count > 0,
            "Client path from the entrance to reception is broken.");
        Require(!IsInsideAnyCollider(
                    colliders,
                    navigation.QueueNode.transform.position),
            "Reception queue node is inside the desk collider.");
    }

    private static void ValidateSortingAndInteraction()
    {
        GameObject desk = RequireObject("AdministratorDesk");
        AdministratorDeskVisualPresenter presenter =
            desk.GetComponent<AdministratorDeskVisualPresenter>();
        ManagerModeController manager =
            UnityEngine.Object.FindAnyObjectByType<ManagerModeController>();
        CameraFollow cameraFollow =
            UnityEngine.Object.FindAnyObjectByType<CameraFollow>();
        Camera camera = cameraFollow != null
            ? cameraFollow.GetComponent<Camera>()
            : null;
        Require(presenter != null && manager != null &&
                cameraFollow != null && camera != null,
            "Administrator desk interaction or camera dependencies are missing.");

        GameObject frontCharacter = CreateTestCharacter(
            "DeskTestFrontCharacter",
            desk.transform.position + new Vector3(0f, -1.20f, 0f),
            ClientType.Regular
        );
        GameObject backCharacter = CreateTestCharacter(
            "DeskTestBackCharacter",
            desk.transform.position + new Vector3(0f, 0.78f, 0f),
            ClientType.Gamer
        );
        SpriteRenderer frontRenderer =
            frontCharacter.GetComponent<SpriteRenderer>();
        SpriteRenderer backRenderer =
            backCharacter.GetComponent<SpriteRenderer>();
        Require(presenter.ShadowRenderer.sortingOrder <
                    presenter.DeskRenderer.sortingOrder &&
                frontRenderer.sortingOrder >
                    presenter.DeskRenderer.sortingOrder &&
                backRenderer.sortingOrder <
                    presenter.DeskRenderer.sortingOrder,
            "Administrator desk Y sorting is invalid for front/back characters.");

        float originalAspect = camera.aspect;
        camera.aspect = 1920f / 1080f;
        Require(cameraFollow.ShowOverview(),
            "Overview failed for the administrator desk pass.");
        CaptureCamera(camera, "Overview_1920x1080", 1920, 1080);

        camera.aspect = 1366f / 768f;
        Require(cameraFollow.ShowOverview(),
            "Overview failed at 1366x768.");
        CaptureCamera(camera, "Overview_1366x768", 1366, 768);

        camera.aspect = 1920f / 1080f;
        Require(cameraFollow.FocusOn(desk.transform),
            "Administrator desk closeup could not be framed.");
        CaptureCamera(camera, "DeskCloseup_1920x1080", 1920, 1080);

        Vector2 clickablePoint = (Vector2)desk.transform.position +
            new Vector2(0f, -0.35f);
        Require(manager.TryFocusAtWorldPosition(clickablePoint) &&
                presenter.IsHovered &&
                !string.IsNullOrWhiteSpace(manager.CurrentPrompt),
            "Administrator desk hover or prompt is broken.");
        Require(manager.TrySelectAtWorldPosition(clickablePoint) &&
                manager.SelectedBehaviour is AdministratorDeskInteraction &&
                presenter.IsSelected,
            "Administrator desk mouse selection is broken.");
        CaptureCamera(camera, "DeskSelected_1920x1080", 1920, 1080);
        Require(manager.TryInteractAtWorldPosition(clickablePoint),
            "Administrator desk interaction dispatch failed.");

        manager.ClearSelection();
        manager.TryFocusAtWorldPosition(new Vector2(-0.5f, -4.8f));
        Require(!presenter.IsSelected && !presenter.IsHovered,
            "Administrator desk highlight did not return to idle.");
        Require(!manager.TrySelectAtWorldPosition(
                    (Vector2)desk.transform.position +
                    new Vector2(0f, 0.32f)),
            "Transparent U-shaped desk center creates a false click target.");

        GameObject terminal = RequireObject("InternetProviderTerminal");
        Require(manager.TrySelectAtWorldPosition(
                    terminal.transform.position) &&
                manager.SelectedBehaviour is InternetProviderTerminal,
            "Nearest reception terminal interaction is broken.");

        PC focusPC = RequireObject("PC_02").GetComponent<PC>();
        Require(focusPC != null &&
                manager.TrySelectAtWorldPosition(focusPC.transform.position) &&
                manager.FocusSelectedObject(),
            "PC Focus was broken by the final desk integration.");
        manager.ClearSelection();
        Require(manager.ShowClubOverview() && !cameraFollow.IsFocused,
            "Overview did not recover after desk interaction checks.");

        ClubResearchPanel researchPanel = ClubResearchPanel.Instance;
        if (researchPanel != null)
        {
            researchPanel.Open();
            Require(researchPanel.IsOpen,
                "Manager research panel did not open.");
            researchPanel.Close();
            Require(!researchPanel.IsOpen,
                "Manager research panel did not close.");
        }

        camera.aspect = originalAspect;
        UnityEngine.Object.Destroy(frontCharacter);
        UnityEngine.Object.Destroy(backCharacter);
    }

    private static GameObject CreateTestCharacter(
        string objectName,
        Vector3 position,
        ClientType type)
    {
        GameObject character = new(objectName);
        character.transform.position = position;
        SpriteRenderer renderer = character.AddComponent<SpriteRenderer>();
        renderer.sprite = WorldVisualPrimitives.SquareSprite;
        renderer.color = Color.clear;
        YSortRenderer.SetSortingLayer(renderer, "World");
        YSortRenderer ySort = YSortRenderer.Ensure(character, 20, -0.45f);
        CharacterVisualPresenter visual =
            character.AddComponent<CharacterVisualPresenter>();
        visual.ConfigureClient(type);
        ySort.RefreshSortingOrder();
        SyncCharacterParts(character, renderer.sortingOrder);
        return character;
    }

    private static void SyncCharacterParts(
        GameObject character,
        int baseOrder)
    {
        foreach (WorldVisualPart part in
                 character.GetComponentsInChildren<WorldVisualPart>(true))
        {
            if (part.TryGetComponent(out SpriteRenderer renderer))
            {
                renderer.sortingOrder = baseOrder + part.OrderOffset;
            }
        }
    }

    private static int CountNamedChildren(
        Transform parent,
        string objectName)
    {
        int count = 0;
        foreach (Transform child in
                 parent.GetComponentsInChildren<Transform>(true))
        {
            if (child != parent && child.name == objectName)
            {
                count++;
            }
        }
        return count;
    }

    private static Bounds GetRendererBounds(GameObject target)
    {
        Bounds bounds = default;
        bool initialized = false;
        foreach (SpriteRenderer renderer in
                 target.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (!renderer.enabled || renderer.sprite == null)
            {
                continue;
            }

            if (!initialized)
            {
                bounds = renderer.bounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }
        Require(initialized, $"{target.name} has no visible renderer bounds.");
        return bounds;
    }

    private static bool ContainsXY(Bounds bounds, Vector3 point)
    {
        return point.x >= bounds.min.x && point.x <= bounds.max.x &&
            point.y >= bounds.min.y && point.y <= bounds.max.y;
    }

    private static bool IsInsideAnyCollider(
        BoxCollider2D[] colliders,
        Vector2 point)
    {
        foreach (BoxCollider2D collider in colliders)
        {
            if (collider != null && collider.OverlapPoint(point))
            {
                return true;
            }
        }
        return false;
    }
    private static void CaptureCamera(
        Camera camera,
        string fileName,
        int width,
        int height)
    {
        string directory = Path.GetFullPath(Path.Combine(
            Application.dataPath,
            "..",
            "TestArtifacts",
            "AdministratorDeskFinalAsset"
        ));
        Directory.CreateDirectory(directory);

        RenderTexture renderTexture = RenderTexture.GetTemporary(
            width,
            height,
            24,
            RenderTextureFormat.ARGB32
        );
        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        Texture2D image = new(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = renderTexture;
            camera.Render();
            camera.Render();
            RenderTexture.active = renderTexture;
            image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(
                Path.Combine(directory, $"{fileName}.png"),
                image.EncodeToPNG()
            );
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(renderTexture);
            UnityEngine.Object.Destroy(image);
        }
    }

    private static GameObject RequireObject(string objectName)
    {
        GameObject result = GameObject.Find(objectName);
        if (result == null)
        {
            throw new InvalidOperationException($"{objectName} was not found.");
        }
        return result;
    }

    private static SpriteRenderer RequireRenderer(string objectName)
    {
        SpriteRenderer renderer = RequireObject(objectName)
            .GetComponent<SpriteRenderer>();
        Require(renderer != null, $"{objectName} has no SpriteRenderer.");
        return renderer;
    }

    private static void CapturePrimaryFingerprint()
    {
        EditorPrefs.SetString(
            PrimaryFingerprintKey,
            GetFileFingerprint(SaveStorageProfile.PrimarySavePath)
        );
    }

    private static void ValidatePrimaryFingerprint()
    {
        string before = EditorPrefs.GetString(
            PrimaryFingerprintKey,
            string.Empty
        );
        string after = GetFileFingerprint(SaveStorageProfile.PrimarySavePath);
        Require(string.Equals(before, after, StringComparison.Ordinal),
            "The primary user save changed during the polish smoke test.");
    }

    private static string GetFileFingerprint(string path)
    {
        if (!File.Exists(path))
        {
            return "missing";
        }

        using SHA256 sha256 = SHA256.Create();
        return Convert.ToBase64String(
            sha256.ComputeHash(File.ReadAllBytes(path))
        );
    }

    private static void BackupQASave()
    {
        if (File.Exists(QASavePath))
        {
            File.Copy(QASavePath, QASaveBackupPath, true);
        }
        else if (File.Exists(QASaveBackupPath))
        {
            File.Delete(QASaveBackupPath);
        }
    }

    private static void RestoreQASave()
    {
        if (File.Exists(QASaveBackupPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(QASavePath));
            File.Copy(QASaveBackupPath, QASavePath, true);
            File.Delete(QASaveBackupPath);
        }
        else if (File.Exists(QASavePath))
        {
            File.Delete(QASavePath);
        }
    }

    private static void OnLogMessageReceived(
        string condition,
        string stackTrace,
        LogType type)
    {
        if ((type == LogType.Error || type == LogType.Exception ||
             type == LogType.Assert) &&
            !condition.StartsWith(
                "ADMINISTRATOR_DESK_FINAL_ASSET_SMOKE_TEST: FAIL") &&
            stackTrace.Contains("Assets/"))
        {
            RuntimeErrors.Add(condition);
        }
    }

    private static void Fail(Exception exception)
    {
        EditorPrefs.SetBool(FailedKey, true);
        Debug.LogError(
            $"ADMINISTRATOR_DESK_FINAL_ASSET_SMOKE_TEST: FAIL\n{exception}"
        );
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
        }
        else
        {
            RestoreQASave();
            EditorPrefs.DeleteKey(PendingKey);
            EditorPrefs.DeleteKey(FailedKey);
            EditorPrefs.DeleteKey(PrimaryFingerprintKey);
            EditorApplication.Exit(1);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
