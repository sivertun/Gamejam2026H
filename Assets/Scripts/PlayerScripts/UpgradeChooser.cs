using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The choice you get each time the lantern reaches a new stage: the game holds still and offers
// three upgrades, and you press 1, 2 or 3 to take one. Builds its own screen in code, like
// DeathSequence does, so no scene needs setting up for it.
public class UpgradeChooser : MonoBehaviour
{
    // Other scripts check this so you can't swing or suck through the pause
    public static bool IsChoosing { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Init()
    {
        // Needed when "Reload Domain" is off in Enter Play Mode options
        IsChoosing = false;
    }

    public static void Offer(GameObject player)
    {
        if (IsChoosing || DeathSequence.IsDead) return;

        PlayerUpgrades upgrades = PlayerUpgrades.Ensure(player);
        List<Upgrade> choices = UpgradePool.Offer(upgrades, 3);
        if (choices.Count == 0) return; // nothing left to offer

        UpgradeChooser chooser = new GameObject("UpgradeChooser").AddComponent<UpgradeChooser>();
        chooser.Begin(upgrades, choices);
    }

    private PlayerUpgrades upgrades;
    private List<Upgrade> choices;

    private void Begin(PlayerUpgrades playerUpgrades, List<Upgrade> offered)
    {
        upgrades = playerUpgrades;
        choices = offered;

        IsChoosing = true;
        Time.timeScale = 0f;
        BuildScreen();
    }

    void Update()
    {
        for (int i = 0; i < choices.Count; i++)
        {
            if (PressedNumber(i + 1))
            {
                Pick(i);
                return;
            }
        }
    }

    private void Pick(int index)
    {
        upgrades.Take(choices[index]);

        IsChoosing = false;
        // Dying mid-choice would be running its own slow motion, so leave that alone
        if (!DeathSequence.IsDead) Time.timeScale = 1f;
        Destroy(gameObject);
    }

    private static bool PressedNumber(int number)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return false;

        Key digit = number == 1 ? Key.Digit1 : number == 2 ? Key.Digit2 : Key.Digit3;
        Key numpad = number == 1 ? Key.Numpad1 : number == 2 ? Key.Numpad2 : Key.Numpad3;
        return keyboard[digit].wasPressedThisFrame || keyboard[numpad].wasPressedThisFrame;
    }

    // ---------- the screen ----------

    private void BuildScreen()
    {
        Transform canvas = DeathSequence.MakeCanvas(transform);

        Image dim = DeathSequence.MakeImage(canvas, "Dim", new Color(0f, 0f, 0f, 0.75f));
        DeathSequence.Stretch(dim.rectTransform);

        Text heading = MakeText(canvas, "Heading", "THE LANTERN GROWS", 66, DisplayFont);
        heading.color = new Color(1f, 0.85f, 0.55f);
        Place(heading.rectTransform, new Vector2(0f, 330f), new Vector2(1500f, 130f));

        Text hint = MakeText(canvas, "Hint", "choose one", 34, BodyFont);
        hint.color = new Color(0.7f, 0.7f, 0.75f);
        Place(hint.rectTransform, new Vector2(0f, 250f), new Vector2(1500f, 60f));

        for (int i = 0; i < choices.Count; i++) BuildCard(canvas, i);
    }

    private void BuildCard(Transform canvas, int index)
    {
        const float spacing = 520f;
        float x = (index - (choices.Count - 1) * 0.5f) * spacing;

        Image card = DeathSequence.MakeImage(canvas, "Card" + index, new Color(0.06f, 0.06f, 0.08f, 0.96f));
        Place(card.rectTransform, new Vector2(x, -40f), new Vector2(480f, 420f));

        // A warm bar along the top, so the cards read as lantern light rather than menu boxes
        Image bar = DeathSequence.MakeImage(card.transform, "Bar", new Color(1f, 0.72f, 0.35f, 0.9f));
        bar.rectTransform.anchorMin = new Vector2(0f, 1f);
        bar.rectTransform.anchorMax = new Vector2(1f, 1f);
        bar.rectTransform.pivot = new Vector2(0.5f, 1f);
        bar.rectTransform.sizeDelta = new Vector2(0f, 8f);
        bar.rectTransform.anchoredPosition = Vector2.zero;

        Text number = MakeText(card.transform, "Number", (index + 1).ToString(), 44, BodyFont);
        number.color = new Color(1f, 0.72f, 0.35f);
        Place(number.rectTransform, new Vector2(0f, 140f), new Vector2(420f, 70f));

        Text title = MakeText(card.transform, "Title", choices[index].Title, 46, DisplayFont);
        Place(title.rectTransform, new Vector2(0f, 60f), new Vector2(420f, 100f));

        Text body = MakeText(card.transform, "Body", choices[index].Description, 28, BodyFont);
        body.color = new Color(0.82f, 0.82f, 0.86f);
        Place(body.rectTransform, new Vector2(0f, -70f), new Vector2(400f, 200f));

        int taken = upgrades.TimesTaken(choices[index].Id);
        if (taken > 0)
        {
            Text stacks = MakeText(card.transform, "Stacks", $"already taken {taken}x", 24, BodyFont);
            stacks.color = new Color(0.55f, 0.55f, 0.6f);
            Place(stacks.rectTransform, new Vector2(0f, -175f), new Vector2(400f, 50f));
        }
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static Text MakeText(Transform parent, string name, string content, int size, Font font)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);

        Text text = go.GetComponent<Text>();
        text.font = font;
        text.text = content;
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        text.color = Color.white;
        return text;
    }

    private static Font BodyFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    private static Font DisplayFont
    {
        get
        {
            Font death = Resources.Load<Font>("DeathFont");
            return death != null ? death : BodyFont;
        }
    }
}
