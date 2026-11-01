using System;
using UnityEngine;

namespace AIPipeline.Runtime
{
    [Serializable]
    public class ItemGenerationDto
    {
        public string itemName;
        public string description;
        public string rarity;
        public int levelRequirement;
        public int attackPower;
        public int goldValue;
        public string sdIconPrompt; // Промпт на английском, который Groq сам напишет для ComfyUI!
    }

    [CreateAssetMenu(fileName = "NewItem", menuName = "AI Pipeline/RPG Item")]
    public class RPGItemConfig : ScriptableObject
    {
        public string ItemName;
        [TextArea(3, 5)] public string Description;
        public string Rarity;
        public int LevelRequirement;
        public int AttackPower;
        public int GoldValue;
        public Sprite Icon;
    }
}