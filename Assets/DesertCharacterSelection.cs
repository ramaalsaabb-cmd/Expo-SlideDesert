using UnityEngine;

public static class DesertCharacterSelection
{
    const string Key = "Desert.SelectedCharacter";
    public static bool MuflehSelected => PlayerPrefs.GetString(Key, "Man") == "Man";
    public static void Save(string choice)
    {
        PlayerPrefs.SetString(Key, choice == "Man" ? "Man" : "Woman");
        PlayerPrefs.Save();
    }
}
