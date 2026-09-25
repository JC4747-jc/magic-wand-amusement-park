using System;

public static class StereoPatchMatcher
{
    public struct Estimate
    {
        public bool valid;
        public float disparityPixels;
        public float depthMeters;
        public float confidence;
        public float validRatio;
        public float verticalOffsetPixels;
        public int acceptedSamples;
        public int attemptedSamples;
        public string reason;
    }

    struct Match
    {
        public bool valid;
        public float disparity;
        public float confidence;
        public int verticalOffset;
    }

    const int PatchRadiusX = 4;
    const int PatchRadiusY = 3;
    const int MinDisparity = 2;
    const int MaxDisparity = 120;
    const int MaxVerticalOffset = 5;
    const float MinimumTexture = 4f;
    const float MinimumConfidence = 0.025f;

    static readonly int[] SampleOffsetX = { -24, 0, 24, -24, 0, 24, -24, 0, 24 };
    static readonly int[] SampleOffsetY = { -16, -16, -16, 0, 0, 0, 16, 16, 16 };

    public static Estimate EstimateDepth(byte[] leftGray, byte[] rightGray, int width, int height,
        int centerX, int centerY, float focalLengthPixels, float baselineMeters, int spreadX = 24, int spreadY = 16)
    {
        var result = new Estimate
        {
            attemptedSamples = SampleOffsetX.Length,
            reason = "No reliable stereo matches"
        };
        if (leftGray == null || rightGray == null || leftGray.Length != width * height ||
            rightGray.Length != width * height || width < 64 || height < 48)
        {
            result.reason = "Invalid grayscale frame buffers";
            return result;
        }
        if (!Finite(focalLengthPixels) || focalLengthPixels <= 0 ||
            !Finite(baselineMeters) || baselineMeters <= 0)
        {
            result.reason = "Invalid stereo calibration";
            return result;
        }

        var disparities = new float[SampleOffsetX.Length];
        var confidences = new float[SampleOffsetX.Length];
        var verticalOffsets = new float[SampleOffsetX.Length];
        int count = 0;
        for (int i = 0; i < SampleOffsetX.Length; i++)
        {
            int x = centerX + Math.Sign(SampleOffsetX[i]) * spreadX;
            int y = centerY + Math.Sign(SampleOffsetY[i]) * spreadY;
            Match match = MatchPoint(leftGray, rightGray, width, height, x, y);
            if (!match.valid) continue;
            disparities[count] = match.disparity;
            confidences[count] = match.confidence;
            verticalOffsets[count] = match.verticalOffset;
            count++;
        }

        if (count < 3)
        {
            result.reason = "Only " + count + "/" + SampleOffsetX.Length + " textured matches";
            return result;
        }

        float medianDisparity = Median(disparities, count);
        float tolerance = Math.Max(2f, medianDisparity * 0.25f);
        var acceptedDisparities = new float[count];
        var acceptedConfidences = new float[count];
        var acceptedVerticalOffsets = new float[count];
        int accepted = 0;
        for (int i = 0; i < count; i++)
        {
            if (Math.Abs(disparities[i] - medianDisparity) > tolerance) continue;
            acceptedDisparities[accepted] = disparities[i];
            acceptedConfidences[accepted] = confidences[i];
            acceptedVerticalOffsets[accepted] = verticalOffsets[i];
            accepted++;
        }

        if (accepted < 3)
        {
            result.reason = "Stereo matches disagree on disparity";
            return result;
        }

        float disparity = Median(acceptedDisparities, accepted);
        float depth = focalLengthPixels * baselineMeters / disparity;
        if (!Finite(depth) || depth < 0.20f || depth > 10f)
        {
            result.reason = "Estimated depth is outside 0.20-10.0 m";
            return result;
        }

        result.valid = true;
        result.disparityPixels = disparity;
        result.depthMeters = depth;
        result.confidence = Median(acceptedConfidences, accepted);
        result.validRatio = accepted / (float)SampleOffsetX.Length;
        result.verticalOffsetPixels = Median(acceptedVerticalOffsets, accepted);
        result.acceptedSamples = accepted;
        result.reason = "OK";
        return result;
    }

    static Match MatchPoint(byte[] left, byte[] right, int width, int height, int x, int y)
    {
        var result = new Match();
        int maxDisparity = Math.Min(MaxDisparity, x - PatchRadiusX - 1);
        if (x - PatchRadiusX < 0 || x + PatchRadiusX >= width ||
            maxDisparity <= MinDisparity || y - PatchRadiusY - MaxVerticalOffset < 0 ||
            y + PatchRadiusY + MaxVerticalOffset >= height)
            return result;

        int sampleCount = (PatchRadiusX * 2 + 1) * (PatchRadiusY * 2 + 1);
        int leftSum = 0;
        for (int py = -PatchRadiusY; py <= PatchRadiusY; py++)
            for (int px = -PatchRadiusX; px <= PatchRadiusX; px++)
                leftSum += left[(y + py) * width + x + px];
        int leftMean = leftSum / sampleCount;
        int texture = 0;
        for (int py = -PatchRadiusY; py <= PatchRadiusY; py++)
            for (int px = -PatchRadiusX; px <= PatchRadiusX; px++)
                texture += Math.Abs(left[(y + py) * width + x + px] - leftMean);
        if (texture / (float)sampleCount < MinimumTexture) return result;

        var costs = new int[maxDisparity + 1];
        var offsets = new int[maxDisparity + 1];
        for (int d = 0; d <= maxDisparity; d++) costs[d] = int.MaxValue;

        for (int disparity = MinDisparity; disparity <= maxDisparity; disparity++)
        {
            int rightX = x - disparity;
            if (rightX - PatchRadiusX < 0) continue;
            for (int dy = -MaxVerticalOffset; dy <= MaxVerticalOffset; dy++)
            {
                int rightY = y + dy;
                int rightSum = 0;
                for (int py = -PatchRadiusY; py <= PatchRadiusY; py++)
                    for (int px = -PatchRadiusX; px <= PatchRadiusX; px++)
                        rightSum += right[(rightY + py) * width + rightX + px];
                int rightMean = rightSum / sampleCount;
                int cost = 0;
                for (int py = -PatchRadiusY; py <= PatchRadiusY; py++)
                {
                    int leftRow = (y + py) * width;
                    int rightRow = (rightY + py) * width;
                    for (int px = -PatchRadiusX; px <= PatchRadiusX; px++)
                    {
                        int a = left[leftRow + x + px] - leftMean;
                        int b = right[rightRow + rightX + px] - rightMean;
                        cost += Math.Abs(a - b);
                    }
                }
                if (cost < costs[disparity])
                {
                    costs[disparity] = cost;
                    offsets[disparity] = dy;
                }
            }
        }

        int bestDisparity = -1;
        int bestCost = int.MaxValue;
        for (int d = MinDisparity; d <= maxDisparity; d++)
        {
            if (costs[d] >= bestCost) continue;
            bestCost = costs[d];
            bestDisparity = d;
        }
        if (bestDisparity <= MinDisparity || bestDisparity >= maxDisparity) return result;

        int secondCost = int.MaxValue;
        for (int d = MinDisparity; d <= maxDisparity; d++)
        {
            if (Math.Abs(d - bestDisparity) <= 2) continue;
            if (costs[d] < secondCost) secondCost = costs[d];
        }
        if (secondCost == int.MaxValue || secondCost <= 0) return result;
        float confidence = (secondCost - bestCost) / (float)secondCost;
        if (confidence < MinimumConfidence) return result;

        float subpixel = 0f;
        int previous = costs[bestDisparity - 1];
        int next = costs[bestDisparity + 1];
        int denominator = previous - 2 * bestCost + next;
        if (previous != int.MaxValue && next != int.MaxValue && denominator != 0)
        {
            subpixel = 0.5f * (previous - next) / denominator;
            if (subpixel < -1f) subpixel = -1f;
            if (subpixel > 1f) subpixel = 1f;
        }

        result.valid = true;
        result.disparity = bestDisparity + subpixel;
        result.confidence = confidence;
        result.verticalOffset = offsets[bestDisparity];
        return result;
    }

    static float Median(float[] values, int count)
    {
        var copy = new float[count];
        Array.Copy(values, copy, count);
        Array.Sort(copy);
        int middle = count / 2;
        return count % 2 == 0 ? (copy[middle - 1] + copy[middle]) * 0.5f : copy[middle];
    }

    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

