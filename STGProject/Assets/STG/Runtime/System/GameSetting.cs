using UnityEngine;


namespace GenjitsuLAB.STG
{
    [CreateAssetMenu(fileName = "GameSettingAsset", menuName = "STG/Game Setting Asset")]
    public class GameSetting : ScriptableObject
    {
        public PlayerController playerPrefab;
        public Transform background;

    }
}