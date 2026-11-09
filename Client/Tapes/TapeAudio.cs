using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using SPT.Custom.Utils;
using UnityEngine;

namespace PlayableMusicTapes.Client;

internal static class TapeAudio
{
    private static readonly Dictionary<string, AssetBundle> LoadedBundles = new Dictionary<string, AssetBundle>();

    public static IEnumerator LoadClip(TapeInfo tape, TrackInfo track, Action<AudioClip> done)
    {
        if (!LoadedBundles.TryGetValue(tape.Bundle, out AssetBundle bundle) || bundle == null)
        {
            if (!BundleManager.Bundles.TryGetValue(tape.Bundle, out var bundleInfo))
            {
                Log.Error("Tapes", "audio bundle " + tape.Bundle + " not registered by the server - is the server mod installed?");
                done(null);
                yield break;
            }

            string path = Path.GetFullPath(BundleManager.GetBundleFilePath(bundleInfo));
            AssetBundleCreateRequest bundleRequest = AssetBundle.LoadFromFileAsync(path);
            yield return bundleRequest;

            bundle = bundleRequest.assetBundle;
            if (bundle == null)
            {
                Log.Error("Tapes", "audio bundle failed to load: " + path);
                done(null);
                yield break;
            }

            LoadedBundles[tape.Bundle] = bundle;
            Log.Info("Tapes", "audio bundle " + tape.TapeId + " loaded from " + path);
        }

        AssetBundleRequest clipRequest = bundle.LoadAssetAsync<AudioClip>(track.Asset);
        yield return clipRequest;

        var clip = clipRequest.asset as AudioClip;
        if (clip == null)
        {
            Log.Error("Tapes", "track " + track.Asset + " missing in " + tape.Bundle);
        }

        done(clip);
    }
}
