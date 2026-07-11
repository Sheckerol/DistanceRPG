using GameEngine.Core;
using GameEngine.Core.UI;
using GameEngine.DistanceRPG.Logic;
using OpenTK.Mathematics;

namespace GameEngine.DistanceRPG;

/// <summary>
/// Screen-space HUD for the dungeon scene, drawn with the engine's bitmap
/// <see cref="TextRenderer"/>: movement/weapon readouts, the party selector,
/// the enemy label, floating combat text, the end-of-turn banner, the
/// inventory panel, and the game-over screen.
/// </summary>
public sealed class DungeonHud
{
    private const float FloatingTextLife = 1.2f;
    private const float FloatingTextRise = 50f;

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Yellow = new(1f, 0.87f, 0f, 1f);
    private static readonly Vector4 Grey = new(0.6f, 0.6f, 0.65f, 1f);
    private static readonly Vector4 DarkGrey = new(0.4f, 0.4f, 0.42f, 1f);
    private static readonly Vector4 Red = new(1f, 0.27f, 0.27f, 1f);
    private static readonly Vector4 Orange = new(0.96f, 0.65f, 0.14f, 1f);
    private static readonly Vector4 Cyan = new(0.31f, 0.76f, 0.97f, 1f);

    private readonly Game _game;
    private readonly TextRenderer _text = new();
    private readonly FullscreenFade _fade = new();
    private bool _glReady;

    private sealed record FloatingText(Vector3 WorldPos, string Text, Vector4 Color, float PixelYOffset)
    {
        public float Age;
    }

    private readonly List<FloatingText> _floatingTexts = new();

    public DungeonHud(Game game)
    {
        _game = game;
    }

    /// <summary>Spawn combat text above a world position, rising and fading out.</summary>
    public void AddFloatingText(Vector3 worldPos, string text, Vector4 color, float pixelYOffset = 0f)
    {
        _floatingTexts.Add(new FloatingText(worldPos, text, color, pixelYOffset));
    }

    public void Update(float deltaTime)
    {
        for (int i = _floatingTexts.Count - 1; i >= 0; i--)
        {
            _floatingTexts[i].Age += deltaTime;
            if (_floatingTexts[i].Age >= FloatingTextLife)
                _floatingTexts.RemoveAt(i);
        }
    }

    public void Render(Camera camera, DungeonScene scene)
    {
        if (!_glReady)
        {
            _text.Initialize();
            _fade.Initialize();
            _glReady = true;
        }

        int w = _game.ClientSize.X;
        int h = _game.ClientSize.Y;
        var turns = scene.Turns;
        var active = scene.ActiveCharacter.State;

        // Dim layers go first so all text draws on top of them.
        if (turns.Phase == TurnPhase.GameOver)
            _fade.Draw(_game.ShaderManager, 0.7f);
        else if (scene.AnyMenuOpen)
            _fade.Draw(_game.ShaderManager, 0.65f);

        DrawTopReadouts(w, active);
        DrawPartySelector(scene);
        DrawEnemyLabel(camera, scene, w, h);
        DrawFloatingTexts(camera, w, h);

        if (turns.Phase == TurnPhase.TurnEnding)
        {
            DrawCentered(w, h / 2f - 40f, "END OF TURN!", 4f, Orange);
            if (scene.LastBankedMovement > 0f)
                DrawCentered(w, h / 2f + 8f, $"+{scene.LastBankedMovement:0} SAVED", 2.5f, White);
        }

        if (scene.InventoryOpen)
            DrawInventory(w, h, active);

        if (scene.PauseMenuOpen)
            DrawMenu(w, h);

        if (turns.Phase == TurnPhase.GameOver)
        {
            DrawCentered(w, h / 2f - 60f, "GAME OVER", 6f, Red);
            DrawCentered(w, h / 2f + 20f, "PRESS ESC TO QUIT", 2f, Grey);
        }
        else
        {
            DrawCentered(w, h - 24f,
                "WASD MOVE - CLICK ENEMY ATTACK - SPACE END TURN - I BAG - TAB/1-4 SWITCH - SCROLL ZOOM",
                1.2f, Grey);
        }

        _text.Flush(_game.ShaderManager);
    }

    // ── Sections ─────────────────────────────────────────────────────────────

    private void DrawTopReadouts(int w, PartyMemberState active)
    {
        DrawCentered(w, 14f, $"[{active.Id}] MOVE: {MathF.Ceiling(active.DistLeft)} / {active.EffectiveMax:0}", 2f, White);

        var weapon = active.EquippedWeapon;
        string weaponLabel = weapon == null
            ? $"[{active.Id}] (NO WEAPON)"
            : $"[{active.Id}] {weapon.Name}  DMG:{weapon.Damage}  RNG:{weapon.Range}  COST:{weapon.Cost}{AbilitySuffix(weapon)}";
        DrawCentered(w, 42f, weaponLabel, 1.5f, Yellow);
    }

    // Party selector layout, shared between drawing and click hit-testing.
    private const float SelectorX = 14f;
    private const float SelectorTop = 130f;
    private const float SelectorRowPitch = 30f;
    private const float SelectorRowBand = 26f; // clickable height within each row's pitch
    private const float SelectorWidth = 150f;

    /// <summary>
    /// Hit-test the party selector rows against a mouse position in window
    /// pixels. The rows double as click-to-select buttons.
    /// </summary>
    public static bool HitPartySelector(Vector2 mouse, int partyCount, out int index)
    {
        index = -1;
        if (mouse.X < SelectorX - 4f || mouse.X > SelectorX + SelectorWidth) return false;
        float rel = mouse.Y - (SelectorTop - 5f);
        if (rel < 0f) return false;
        int row = (int)(rel / SelectorRowPitch);
        if (row >= partyCount || rel - row * SelectorRowPitch > SelectorRowBand) return false;
        index = row;
        return true;
    }

    private void DrawPartySelector(DungeonScene scene)
    {
        int hovered = -1;
        bool hasHover = !scene.AnyMenuOpen
            && HitPartySelector(scene.MousePos, scene.Party.Count, out hovered);

        for (int i = 0; i < scene.Party.Count; i++)
        {
            var state = scene.Party[i].State;
            bool isActive = i == scene.ActiveIndex;
            var color = !state.Alive ? DarkGrey : scene.PartyColor(i);
            if (hasHover && hovered == i && state.Alive)
                color = Vector4.Lerp(color, White, 0.5f);
            string marker = isActive ? ">" : " ";
            _text.DrawText($"{marker}{i + 1} {state.Id} {state.Hp}", SelectorX, SelectorTop + i * SelectorRowPitch, 2f, color);
        }
    }

    private void DrawEnemyLabel(Camera camera, DungeonScene scene, int w, int h)
    {
        if (!scene.EnemyVisible || !scene.Enemy.State.Alive) return;
        var anchor = scene.Enemy.Position + Vector3.UnitY * 0.9f;
        if (!WorldToScreen(camera, anchor, w, h, out var px)) return;

        var enemy = scene.Enemy.State;
        DrawCenteredAt(px.X, px.Y - 18f, $"DUMMY [{enemy.Weapon.Name}]", 1.5f, Orange);
        DrawCenteredAt(px.X, px.Y, $"{enemy.Hp}/{enemy.MaxHp}", 1.5f, White);
    }

    private void DrawFloatingTexts(Camera camera, int w, int h)
    {
        foreach (var ft in _floatingTexts)
        {
            if (!WorldToScreen(camera, ft.WorldPos + Vector3.UnitY * 1.2f, w, h, out var px)) continue;
            float t = ft.Age / FloatingTextLife;
            float alpha = 1f - t * t;
            float y = px.Y + ft.PixelYOffset - FloatingTextRise * t;
            DrawCenteredAt(px.X, y, ft.Text, 2f, ft.Color with { W = alpha });
        }
    }

    private void DrawInventory(int w, int h, PartyMemberState active)
    {
        float cx = w / 2f;
        float top = h / 2f - 150f;

        DrawCentered(w, top, $"INVENTORY - CHAR {active.Id}", 3f, Cyan);
        _text.DrawText("EQUIPPED", cx - 190f, top + 46f, 1.2f, Grey);

        for (int slot = 0; slot < active.Inventory.Length; slot++)
        {
            float y = top + 70f + slot * 46f;
            var weapon = active.Inventory[slot];
            var color = slot == 0 ? Yellow : White;
            string label = weapon == null
                ? $"{slot + 1}  - EMPTY -"
                : $"{slot + 1}  {weapon.Name}  DMG:{weapon.Damage} RNG:{weapon.Range} COST:{weapon.Cost}{AbilitySuffix(weapon)}";
            _text.DrawText(label, cx - 190f, y, 2f, weapon == null ? DarkGrey : color);
        }

        DrawCentered(w, top + 70f + 3 * 46f + 20f, "PRESS 2-3 TO EQUIP - I TO CLOSE", 1.5f, Grey);
    }

    private void DrawMenu(int w, int h)
    {
        float top = h / 2f - 90f;
        DrawCentered(w, top, "PAUSED", 4f, Cyan);
        DrawCentered(w, top + 66f, "1  CLOSE", 2f, White);
        DrawCentered(w, top + 106f, "2  EXIT GAME", 2f, White);
        DrawCentered(w, top + 156f, "ESC TO CLOSE", 1.5f, Grey);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string AbilitySuffix(Weapon weapon)
    {
        var parts = weapon.Abilities.Select(a => a.Type switch
        {
            AbilityType.Block => $"BLOCK {a.Value}",
            AbilityType.CritRange => $"CRIT +{a.Value}",
            AbilityType.Brace => "BRACE",
            _ => a.Type.ToString().ToUpperInvariant(),
        }).ToList();
        return parts.Count > 0 ? "  * " + string.Join("  ", parts) : "";
    }

    private void DrawCentered(int viewportW, float y, string text, float scale, Vector4 color)
        => DrawCenteredAt(viewportW / 2f, y, text, scale, color);

    private void DrawCenteredAt(float cx, float y, string text, float scale, Vector4 color)
    {
        float width = text.Length * 6f * scale; // 5px glyph + 1px spacing
        _text.DrawText(text, cx - width / 2f, y, scale, color);
    }

    private static bool WorldToScreen(Camera camera, Vector3 world, int w, int h, out Vector2 pixels)
    {
        var clip = new Vector4(world, 1f) * (camera.ViewMatrix * camera.ProjectionMatrix);
        if (clip.W <= 0.001f)
        {
            pixels = default;
            return false;
        }
        var ndc = clip.Xyz / clip.W;
        pixels = new Vector2((ndc.X * 0.5f + 0.5f) * w, (1f - (ndc.Y * 0.5f + 0.5f)) * h);
        return true;
    }

    public void Dispose()
    {
        _text.Dispose();
        _fade.Dispose();
    }
}
