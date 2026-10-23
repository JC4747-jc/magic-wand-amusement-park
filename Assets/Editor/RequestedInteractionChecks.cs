using UnityEngine;
public static class RequestedInteractionChecks
{
    public static void Run()
    {
        RightHandSpellVfxChecks.Run();
        StereoIntegrationChecks.Run();
        Debug.Log("MAIN_LIGHTER_REGRESSION: main gesture, follow, registration, VFX/reset checks");
    }
}
