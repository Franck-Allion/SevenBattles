namespace SevenBattles.Core.Preload
{
    public struct PreloadResult
    {
        public bool Success;
        public float DurationMs;
        public string ErrorMessage;

        public static PreloadResult Ok(float durationMs)
        {
            return new PreloadResult
            {
                Success = true,
                DurationMs = durationMs,
                ErrorMessage = null
            };
        }

        public static PreloadResult Fail(float durationMs, string error)
        {
            return new PreloadResult
            {
                Success = false,
                DurationMs = durationMs,
                ErrorMessage = error
            };
        }
    }
}
