using System.IO;
using UnityEditor;
using UnityEngine;

namespace R3Samples.EditorTool
{
    public class SpritePaddingBatch
    {
        [MenuItem("Tools/Sprites/Add Transparent Padding To Selected PNGs")]
        private static void AddPaddingToSelected()
        {
            const int padding = 16; // 左右上下の余白px
            const string outputFolder = "Assets/PaddedSprites";

            if (!AssetDatabase.IsValidFolder(outputFolder))
            {
                AssetDatabase.CreateFolder("Assets", "PaddedSprites");
            }

            var objects = Selection.objects;
            if (objects == null || objects.Length == 0)
            {
                Debug.LogWarning("Texture2D または PNG を選択してください。");
                return;
            }

            foreach (var obj in objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path) || Path.GetExtension(path).ToLower() != ".png")
                    continue;

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    continue;

                bool prevReadable = importer.isReadable;
                TextureImporterCompression prevCompression = importer.textureCompression;

                bool needReimport = !prevReadable || prevCompression != TextureImporterCompression.Uncompressed;
                if (needReimport)
                {
                    importer.isReadable = true;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                }

                Texture2D src = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (src == null)
                    continue;

                Texture2D dst = new Texture2D(
                    src.width + padding * 2,
                    src.height + padding * 2,
                    TextureFormat.RGBA32,
                    false
                );

                Color32[] clearPixels = new Color32[dst.width * dst.height];
                for (int i = 0; i < clearPixels.Length; i++)
                    clearPixels[i] = new Color32(0, 0, 0, 0);

                dst.SetPixels32(clearPixels);
                dst.SetPixels(padding, padding, src.width, src.height, src.GetPixels());
                dst.Apply();

                string fileName = Path.GetFileNameWithoutExtension(path) + "_padded.png";
                string outPath = Path.Combine(outputFolder, fileName).Replace("\\", "/");
                File.WriteAllBytes(outPath, dst.EncodeToPNG());

                Object.DestroyImmediate(dst);

                if (needReimport)
                {
                    importer.isReadable = prevReadable;
                    importer.textureCompression = prevCompression;
                    importer.SaveAndReimport();
                }
            }

            AssetDatabase.Refresh();
            Debug.Log("余白付きPNGを書き出しました: " + outputFolder);
        }
    }
}