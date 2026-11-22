namespace Perception
{
    /// <summary>Image recognition freshness and spatial registration are independent gates.</summary>
    public static class RecognitionReplyPolicy
    {
        public static bool AcceptRecognition(long frame,long expected,long previous,int width,int height,float age,float limit)
            => frame==expected && frame>previous && width==640 && height==480 && float.IsFinite(age) && age>=0 && age<=limit;
        public static bool AcceptSpatial(float age,float limit,bool capturePoseValid,bool headTracked)
            => capturePoseValid && headTracked && float.IsFinite(age) && age>=0 && age<=limit;
    }
}
