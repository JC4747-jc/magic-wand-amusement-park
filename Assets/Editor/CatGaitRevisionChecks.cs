using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CatGaitRevisionChecks
{
    public static void Run()
    {
        const string output=".codex-tmp/cat-gait-revision";
        Directory.CreateDirectory(output);File.WriteAllText(output+"/result.txt","RUNNING\n");
        try
        {
            var clips=AssetDatabase.LoadAllAssetsAtPath(BicolorCatAssetBuilder.Folder+"/BicolorCat.fbx").OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview")).ToArray();
            foreach(string expected in new[]{"BicolorCat_Idle","BicolorCat_Feed","BicolorCat_Walk"})
                if(clips.Count(c=>c.name.EndsWith(expected,StringComparison.Ordinal))!=1)throw new Exception("Cat clip reference missing or duplicated: "+expected);
            NaturalPetModelChecks.Run();NaturalCatWalkChecks.Run();NaturalDogModelChecks.Run();
            File.WriteAllText(output+"/result.txt","PASS: current cat FBX and existing prefab/controller references; original 8646-triangle / 40-bone budget and three skins, original albedo and UV0 normal map; finite idle/sniff placement and 193 imported walk skin samples at confirmed 0.35m height / 37 degree yaw; four paw lift/contact, loop seam and fixed tracking root; original dog model regression. Authored in-place preview only; not measured animal motion, complete eating/sleeping or real YOLO/PICO acceptance.\n");
        }
        catch(Exception e){File.WriteAllText(output+"/result.txt","FAIL: "+e);throw;}
    }
}
