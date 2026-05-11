using System;
using UnityEngine;

namespace GameDemo.DataConfig
{
    public sealed class AssetManagerTextProvider : IConfigTextProvider
    {
        readonly IAssetLoader _assetLoader;

        public AssetManagerTextProvider(IAssetLoader assetLoader = null)
        {
            _assetLoader = assetLoader ?? new AssetManagerLoader();
        }

        public bool TryGetText(string resourcePath, out string text, out string errorMessage)
        {
            text = null;
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                errorMessage = "Resource path is null or empty.";
                return false;
            }

            try
            {
                TextAsset asset = _assetLoader.Load<TextAsset>(resourcePath);
                if (asset == null)
                {
                    errorMessage = $"TextAsset not found at path: \"{resourcePath}\".";
                    return false;
                }

                text = asset.text;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }
    }
}
