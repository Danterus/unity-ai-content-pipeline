using System.IO;
using AIPipeline.Runtime;
using UnityEditor;
using UnityEngine;

namespace AIPipeline.Editor
{
    public static class ItemAssetFactory
    {
        public static RPGItemConfig CreateItemAsset(ItemGenerationDto dto, byte[] iconPngBytes, string outputFolder = "Assets/GeneratedItems")
        {
            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);

            string safeName = string.Join("_", dto.itemName.Split(Path.GetInvalidFileNameChars()));
            string iconPath = $"{outputFolder}/{safeName}_Icon.png";
            string assetPath = $"{outputFolder}/{safeName}_Config.asset";

            File.WriteAllBytes(iconPath, iconPngBytes);
            AssetDatabase.ImportAsset(iconPath, ImportAssetOptions.ForceSynchronousImport);

            if (AssetImporter.GetAtPath(iconPath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            Sprite generatedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);

            var itemConfig = ScriptableObject.CreateInstance<RPGItemConfig>();
            itemConfig.ItemName = dto.itemName;
            itemConfig.Description = dto.description;
            itemConfig.Rarity = dto.rarity;
            itemConfig.LevelRequirement = dto.levelRequirement;
            itemConfig.AttackPower = dto.attackPower;
            itemConfig.GoldValue = dto.goldValue;
            itemConfig.Icon = generatedSprite;

            AssetDatabase.CreateAsset(itemConfig, assetPath);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(itemConfig);
            Selection.activeObject = itemConfig;

            return itemConfig;
        }
    }
}