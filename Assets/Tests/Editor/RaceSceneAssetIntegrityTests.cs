using NUnit.Framework;

namespace GMTK.Tests.Editor
{
    public sealed class RaceSceneAssetIntegrityTests
    {
        private const string RaceScenePath = "Assets/Scenes/GMTK_Race.unity";

        [Test]
        public void RaceScene_DoesNotReferenceCorruptTerrainPrefab()
        {
            string sceneText = System.IO.File.ReadAllText(RaceScenePath);
            StringAssert.DoesNotContain(
                "6bc3a10413094414fa4ef1a873a0c11f",
                sceneText,
                "GMTK_Race must not instantiate the corrupted Terrain.prefab");
        }

        [Test]
        public void RaceScene_DoesNotDependOnStarterKitSampleLightingData()
        {
            string sceneText = System.IO.File.ReadAllText(RaceScenePath);
            StringAssert.DoesNotContain(
                "7d1610789d42d46dd99bd739b39c186b",
                sceneText,
                "GMTK_Race must not reference SampleScene/LightingData.asset");
        }
    }
}
