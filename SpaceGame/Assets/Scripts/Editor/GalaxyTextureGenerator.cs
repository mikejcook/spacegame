using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Generates a procedural spiral galaxy texture and saves it to
/// Assets/Resources/GalaxyBackground.png.
///
/// Menu item: Star Captain → Generate Galaxy Texture
///
/// Algorithm:
///   • Two logarithmic spiral arms with Gaussian angular falloff
///   • Warm elliptical central bulge
///   • Perlin noise gives dusty/cloudy arm texture
///   • Scattered background star field in the disk
///   • Colour: warm yellow-white core → blue-white arms → dim blue outer disk
/// </summary>
public static class GalaxyTextureGenerator
{
    const int   Size       = 1024;
    const string OutputPath = "Assets/Resources/GalaxyBackground.png";

    [MenuItem("Star Captain/Generate Galaxy Texture")]
    public static void Generate()
    {
        var pixels = new Color[Size * Size];

        for (int py = 0; py < Size; py++)
        {
            for (int px = 0; px < Size; px++)
            {
                // Normalised coordinates: -1 to +1
                float nx = (px / (float)Size - 0.5f) * 2f;
                float ny = (py / (float)Size - 0.5f) * 2f;
                float r  = Mathf.Sqrt(nx * nx + ny * ny);

                if (r > 1.05f)
                {
                    pixels[py * Size + px] = Color.clear;
                    continue;
                }

                float theta = Mathf.Atan2(ny, nx);  // -π … +π

                // ── Central bulge ─────────────────────────────────────────
                // Slightly elliptical for a bar-spiral feel
                float bx       = nx * 0.75f;
                float by       = ny * 1.30f;
                float coreR    = Mathf.Sqrt(bx * bx + by * by);
                float core     = Mathf.Exp(-coreR * coreR * 22f);
                float coreBright = Mathf.Exp(-r * r * 6f);

                // ── Two logarithmic spiral arms ───────────────────────────
                // θ_arm = θ – k·ln(r + ε)   (k = tightness; ε avoids ln(0))
                const float Tightness = 3.8f;
                const float BaseWidth = 0.20f;
                const float Epsilon   = 0.12f;

                float armStrength = 0f;
                for (int arm = 0; arm < 2; arm++)
                {
                    float phase       = arm * Mathf.PI;
                    float spiralAngle = theta
                                       - Tightness * Mathf.Log(r + Epsilon)
                                       - phase;
                    // Wrap to [−π, π]
                    spiralAngle = Mathf.Atan2(Mathf.Sin(spiralAngle),
                                              Mathf.Cos(spiralAngle));
                    // Arms widen with radius
                    float width = BaseWidth * (0.5f + r * 0.9f);
                    float val   = Mathf.Exp(-spiralAngle * spiralAngle
                                            / (2f * width * width));
                    armStrength = Mathf.Max(armStrength, val);
                }

                // ── Radial envelope: arms fade at core and at rim ─────────
                float innerFade = Mathf.SmoothStep(0f, 0.20f, r);
                float outerFade = Mathf.SmoothStep(1.04f, 0.50f, r);
                float radialFade = innerFade * outerFade;

                // ── Diffuse disk (thin background glow between arms) ──────
                float disk = Mathf.Exp(-r * r / 0.50f) * 0.10f;

                // ── Perlin noise for dusty arm texture ────────────────────
                // Two octaves; offsets chosen to avoid the 0,0 flat spot
                float n1 = Mathf.PerlinNoise(nx * 3.8f + 73.4f, ny * 3.8f + 73.4f);
                float n2 = Mathf.PerlinNoise(nx * 8.0f + 51.7f, ny * 8.0f + 51.7f);
                float noise = n1 * 0.65f + n2 * 0.35f;

                // Fine dust-lane darkening within arms
                float dustNoise = Mathf.PerlinNoise(nx * 14f + 99f, ny * 14f + 99f);
                float dustLane  = 1f - dustNoise * 0.28f * armStrength * radialFade;

                // ── Sparse star field in the disk ─────────────────────────
                float starNoise = Mathf.PerlinNoise(nx * 62f + 33f, ny * 62f + 33f);
                float rawStar   = Mathf.Max(0f, starNoise - 0.80f) / 0.20f;
                float diskStars = rawStar * rawStar * 0.45f
                                  * Mathf.Exp(-r * r / 0.65f);

                // ── Combine luminances ────────────────────────────────────
                float armLum  = armStrength * radialFade
                                * (0.30f + noise * 0.50f) * dustLane;
                float coreLum = coreBright * 0.88f + core * 0.55f;
                float totalLum = Mathf.Clamp01(coreLum + armLum + disk + diskStars);

                // ── Colour ────────────────────────────────────────────────
                // Core:       warm white-yellow
                // Arms:       blue-white
                // Outer disk: dim blue
                var coreColour = new Color(1.00f, 0.94f, 0.78f);
                var armColour  = new Color(0.70f, 0.84f, 1.00f);
                var diskColour = new Color(0.38f, 0.48f, 0.80f);

                float coreFrac = Mathf.Clamp01(coreLum  / (totalLum + 0.001f));
                float armFrac  = Mathf.Clamp01(armLum   / (totalLum + 0.001f));

                Color baseCol = Color.Lerp(diskColour, armColour, Mathf.Clamp01(armFrac * 1.8f));
                baseCol       = Color.Lerp(baseCol,    coreColour, coreFrac);

                // Pre-multiplied alpha — fades cleanly to transparent at the rim
                float alpha = Mathf.Clamp01(totalLum * 1.8f);
                pixels[py * Size + px] = new Color(
                    baseCol.r * totalLum,
                    baseCol.g * totalLum,
                    baseCol.b * totalLum,
                    alpha);
            }
        }

        // ── Write PNG ─────────────────────────────────────────────────────
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        tex.SetPixels(pixels);
        tex.Apply();

        string fullPath = Path.Combine(Application.dataPath, "Resources", "GalaxyBackground.png");
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
        File.WriteAllBytes(fullPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.Refresh();

        // ── Fix import settings ───────────────────────────────────────────
        var importer = AssetImporter.GetAtPath(OutputPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType    = TextureImporterType.Default;
            importer.mipmapEnabled  = false;
            importer.wrapMode       = TextureWrapMode.Clamp;
            importer.filterMode     = FilterMode.Bilinear;
            importer.maxTextureSize = Size;
            importer.SaveAndReimport();
        }

        EditorUtility.DisplayDialog(
            "Galaxy texture generated!",
            $"Saved to {OutputPath}\n\n" +
            "Use 'Star Captain → Setup Game Scene' to rebuild the scene " +
            "if this is the first time generating.",
            "OK");

        Debug.Log($"[GalaxyTextureGenerator] Texture written to {OutputPath}");
    }
}
