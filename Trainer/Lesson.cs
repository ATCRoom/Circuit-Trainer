namespace ATCRoom.TwrTrainer
{
    /// <summary>
    /// An aircraft as the student sees it (the user, 6 and 9 Nov): callsign, ICAO type, flight rules (well visible), where it
    /// is, altitude, speed and track; nothing of the instructor's (leg, circuit, orders).
    /// </summary>
    internal sealed record StudentTarget(string Callsign, string Type, bool Ifr, double X, double Y, double AltitudeFt, double GroundSpeedKt,
        double Track, bool OnGround, bool Helicopter, int Formation);

    /// <summary>
    /// The "photo" of the lesson made each second (the user, 6 Nov): what the student sees. Used by the student console on
    /// the same PC; later sent on the link between two PCs and written in the recording.
    /// </summary>
    internal sealed record LessonSnapshot(double Time, DateTime Utc, bool Paused, string Aerodrome, string Runway, string Metar,
        IReadOnlyList<StudentTarget> Traffic,
        IReadOnlyList<(string Callsign, double X, double Y, double AltitudeFt, double SpeedKt, double Track)> Network,
        List<StripData> Coordination, List<StripData> Contact);
}
