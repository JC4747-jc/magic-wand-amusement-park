"""Read-only signature, ZIP, identity and scene-count checks for the three APKs."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import zipfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--unity-data', type=Path, required=True)
    parser.add_argument('--include-all-scenes',action='store_true',help='Also verify the separate 14-scene compilation artifact')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    android = args.unity_data/'PlaybackEngines/AndroidPlayer'
    build_tools = android/'SDK/build-tools/34.0.0'
    results = []
    variants=[('BridgeStereoFusion','com.yn.picmagicmr.stereo',7),
                                  ('BridgeStereoPoseFusion','com.yn.picmagicmr.stereo.pose',1),
                                  ('BridgeStereoRepair','com.yn.picmagicmr.stereo.repair',1)]
    if args.include_all_scenes:variants.append(('AllScenesValidation','com.yn.picmagicmr.validation',14))
    for name, package, scenes in variants:
        apk = root/'Builds/Android'/(name+'.apk')
        digest = hashlib.sha256()
        with apk.open('rb') as stream:
            for chunk in iter(lambda:stream.read(1024*1024),b''):
                digest.update(chunk)
        manifest=json.loads((root/'.codex-tmp'/('android-assets-'+name+'.json')).read_text(encoding='utf-8'))
        assert manifest['apk']==apk.name and manifest['sha256']==digest.hexdigest(), 'BuildReport manifest belongs to a different APK'
        required={
            'Assets/ThirdParty/NaturalTrees/SmallTree/SmallTree.fbx', 'Assets/MagicMR/NaturalTreeLeaves.shader',
            'Assets/ThirdParty/NaturalTrees/SmallTree/leaves_alpha.jpg', 'Assets/ThirdParty/NaturalPlants/Haworthia/Haworthia.fbx',
            'Assets/ThirdParty/NaturalDecor/Statue/MarbleBust.fbx', 'Assets/Resources/MagicMR/NaturalMaterials/Wood.mat',
            'Assets/Resources/MagicMR/NaturalMaterials/Linen.mat', 'Assets/Resources/MagicMR/NaturalMaterials/Plaster.mat',
            'Assets/MagicMR/NaturalInsects/Cockroach.fbx', 'Assets/MagicMR/NaturalInsects/Spider.fbx',
            'Assets/MagicMR/NaturalCuticle.shader', 'Assets/ThirdParty/NaturalPets/BicolorCat/BicolorCat.fbx',
            'Assets/ThirdParty/NaturalPets/BicolorCat/Image_0.png', 'Assets/ThirdParty/NaturalPets/BicolorCat/BicolorCat_UV0_Normal.png',
            'Assets/ThirdParty/NaturalPets/Wolfhound/Wolfhound.fbx', 'Assets/ThirdParty/NaturalPets/Wolfhound/Wolfhound_Albedo.png',
            'Assets/ThirdParty/NaturalDecor/ReadingChair/ReadingChair.fbx','Assets/ThirdParty/NaturalDecor/CeramicVase/CeramicVase.fbx',
            'Assets/ThirdParty/NaturalDecor/ReadingChair/modern_arm_chair_01_legs_diff_1k.jpg','Assets/ThirdParty/NaturalDecor/ReadingChair/modern_arm_chair_01_legs_nor_gl_1k.jpg','Assets/ThirdParty/NaturalDecor/ReadingChair/modern_arm_chair_01_legs_URP_MetalAO_Smooth.png',
            'Assets/ThirdParty/NaturalDecor/ReadingChair/modern_arm_chair_01_pillow_diff_1k.jpg','Assets/ThirdParty/NaturalDecor/ReadingChair/modern_arm_chair_01_pillow_nor_gl_1k.jpg','Assets/ThirdParty/NaturalDecor/ReadingChair/modern_arm_chair_01_pillow_URP_MetalAO_Smooth.png',
            'Assets/ThirdParty/NaturalDecor/CeramicVase/ceramic_vase_01_diff_1k.jpg','Assets/ThirdParty/NaturalDecor/CeramicVase/ceramic_vase_01_nor_gl_1k.jpg','Assets/ThirdParty/NaturalDecor/CeramicVase/ceramic_vase_01_URP_MetalAO_Smooth.png',
            'Assets/Resources/MagicMR/NaturalMaterials/RoomMetal.mat','Assets/Resources/MagicMR/NaturalMaterials/BookCloth.mat','Assets/Resources/MagicMR/NaturalMaterials/BookPaper.mat',
            'Assets/MagicMR/NaturalJellyfish/Jellyfish.mesh.asset', 'Assets/MagicMR/NaturalJellyfish.shader',
            'Assets/MagicMR/NaturalTreeScar/Scar.mesh.asset','Assets/MagicMR/NaturalTreeScar/Scar.mat','Assets/MagicMR/NaturalBarkMark.shader',
            'Assets/ThirdParty/NaturalDecor/BronzeBeast/BronzeBeast.fbx','Assets/ThirdParty/NaturalDecor/BronzeBeast/Bronze.mat',
            'Assets/ThirdParty/NaturalDecor/BronzeBeast/bull_head_diff_1k.jpg','Assets/ThirdParty/NaturalDecor/BronzeBeast/bull_head_nor_gl_1k.jpg','Assets/ThirdParty/NaturalDecor/BronzeBeast/Bronze_MetalAO_Smooth.png'}
        assert required.issubset(manifest['sourceAssets']), 'Accepted natural sources missing from BuildReport'
        assert not any(p.startswith('Assets/ThirdParty/NaturalPets/') and not p.startswith(('Assets/ThirdParty/NaturalPets/BicolorCat/','Assets/ThirdParty/NaturalPets/Wolfhound/')) for p in manifest['sourceAssets']), 'Unaccepted pet in BuildReport'
        assert 'Assets/ThirdParty/NaturalPets/BicolorCat/Image_2.png' not in manifest['sourceAssets'], 'Wrong UV2 normal map packed'
        assert not any(p.startswith(('Assets/ThirdParty/NaturalDecor/ReadingChair/','Assets/ThirdParty/NaturalDecor/CeramicVase/','Assets/ThirdParty/NaturalDecor/BronzeBeast/')) and p.endswith('_arm_1k.jpg') for p in manifest['sourceAssets']), 'Unused original ARM map packed'
        with zipfile.ZipFile(apk) as archive:
            assert archive.testzip() is None, 'APK ZIP CRC failure: '+name
            credits=archive.read('assets/MagicMRAssetCredits.txt').decode('utf-8')
            assert all(token in credits for token in ['kenchoo','guillaume bolis','https://creativecommons.org/licenses/by/4.0/','UV2 normals reprojected']), 'Cat attribution or changes absent from APK'
            assert 'four-beat in-place walk' in credits, 'Authored cat gait modification absent from APK credits'
            assert all(token in credits for token in ['Wildfire Games','https://creativecommons.org/licenses/by-sa/3.0/','restrained sniff','61a3b9507d974084e6badb88a0826bd89a6d5b8b']), 'Wolfhound attribution or changes absent from APK'
            assert all(token in credits for token in ['Modern Arm Chair 01','Vibrant Nordic','Ceramic Vase 01','R=metal, G=AO, A=smoothness']), 'Room prop provenance/conversion absent from APK'
            assert all(token in credits for token in ['Bull Head by Tina','two-bone virtual head/neck rig','not an identified museum artifact']), 'Bronze source/rig modification attribution absent from APK'
            levels = sorted(n for n in archive.namelist() if re.fullmatch(r'assets/bin/Data/level\d+',n))
            assert len(levels)==scenes, (name,levels)
            game_settings=archive.getinfo('assets/bin/Data/globalgamemanagers')
            assert game_settings.file_size<16*1024*1024, 'Unexpectedly large global scene settings'
            with archive.open(game_settings) as stream:settings=stream.read(16*1024*1024)
            packed_scenes=sorted({value.decode('utf-8') for value in re.findall(rb'Assets/Scenes/[A-Za-z0-9_]+\.unity',settings)})
            if name=='AllScenesValidation':expected_scenes=sorted('Assets/Scenes/'+p.name for p in (root/'Assets/Scenes').glob('*.unity'))
            elif name=='BridgeStereoFusion':expected_scenes=sorted('Assets/Scenes/'+scene+'.unity' for scene in ['BridgeStereoFusion','ParkTree','ParkBugs','ParkStatue','ParkRoom','ParkPlant','ParkPet'])
            else:expected_scenes=['Assets/Scenes/'+name+'.unity']
            assert len(expected_scenes)==scenes and packed_scenes==expected_scenes, (name,'Packed scene names differ',packed_scenes)
            # Rejected visual candidates must stay outside the player build.
            natural_assets = set()
            pet_animations = set()
            for entry in archive.infolist():
                if not entry.filename.startswith('assets/bin/Data/'):
                    continue
                tail = b''
                with archive.open(entry) as stream:
                    for chunk in iter(lambda:stream.read(1024*1024),b''):
                        data=tail+chunk
                        assert b'NaturalCat' not in data, 'Rejected Innerscene cat candidate leaked into APK'
                        lower_data = data.lower()
                        for token in (b'Haworthia', b'MarbleBust', b'wood_table_001', b'rough_linen', b'beige_wall_002'):
                            if token.lower() in lower_data:
                                natural_assets.add(token.decode())
                        for token in (b'BicolorCat_Walk',b'Wolfhound_WalkSource'):
                            if token.lower() in lower_data:pet_animations.add(token.decode())
                        tail=data[-64:]
            assert len(natural_assets)==5, (name, 'Accepted natural assets missing', natural_assets)
            assert len(pet_animations)==2, (name, 'Pet walk clips absent from player data',pet_animations)
        badging = subprocess.run([str(build_tools/'aapt.exe'),'dump','badging',str(apk)],
                                 check=True,capture_output=True,text=True,encoding='utf-8',errors='replace').stdout
        assert "package: name='"+package+"'" in badging, name
        assert "native-code: 'arm64-v8a'" in badging, name
        signature = subprocess.run([str(android/'OpenJDK/bin/java.exe'),'-jar',str(build_tools/'lib/apksigner.jar'),
                                    'verify','--verbose',str(apk)],check=True,capture_output=True,text=True).stdout
        results.append(dict(apk=apk.name,bytes=apk.stat().st_size,sha256=digest.hexdigest(),package=package,
                            scene_count=len(levels),packed_scenes=packed_scenes,crc_pass=True,signature_pass=True,unaccepted_candidates_absent=True,
                            natural_assets=sorted(natural_assets),packed_pet_animations=sorted(pet_animations),packed_source_assets_pass=True,packed_required_sources=sorted(required)))
    output = root/'.codex-tmp/apk-validation.json'
    output.write_text(json.dumps(results,indent=2),encoding='utf-8')
    print(json.dumps(results,indent=2))


if __name__ == '__main__':
    main()
