using System.Diagnostics;
using System.Globalization;
using System.Runtime;
using System.Runtime.InteropServices;
using PacketWire.Security;

namespace PacketWire.Performance;

/// <summary>
/// Provides micro-benchmark and performance evaluation routines measuring execution time and allocations for serialization and deserialization in both plain and protected modes.
/// </summary>
internal static class Program
{
    /// <summary>
    /// The number of iterations executed during JIT warm-up prior to benchmark measurement.
    /// </summary>
    private const int WarmupIterations =
        50_000;

    /// <summary>
    /// The number of iterations executed during plain serialization and deserialization measurements.
    /// </summary>
    private const int PlainMeasurementIterations =
        500_000;

    /// <summary>
    /// The number of iterations executed during protected AES-GCM serialization and deserialization measurements.
    /// </summary>
    private const int ProtectedMeasurementIterations =
        100_000;

    /// <summary>
    /// The sample packet instance used across benchmark measurement loops.
    /// </summary>
    private static readonly PerformancePacket Packet =
        new()
        {
            Number = 0x11223344,
            Sequence = 0x0102030405060708,
            Enabled = true
        };

    /// <summary>
    /// The static 256-bit cryptographic key used for authenticated payload protection in benchmarks.
    /// </summary>
    private static readonly byte[] Key =
    [
        0x00,
        0x01,
        0x02,
        0x03,
        0x04,
        0x05,
        0x06,
        0x07,
        0x08,
        0x09,
        0x0A,
        0x0B,
        0x0C,
        0x0D,
        0x0E,
        0x0F,
        0x10,
        0x11,
        0x12,
        0x13,
        0x14,
        0x15,
        0x16,
        0x17,
        0x18,
        0x19,
        0x1A,
        0x1B,
        0x1C,
        0x1D,
        0x1E,
        0x1F
    ];

    /// <summary>
    /// Entry point for the performance benchmark harness.
    /// </summary>
    /// <returns>Zero if execution completed successfully.</returns>
    public static int Main()
    {
        using AesGcmPayloadProtector protector =
            new(
                Key);

        byte[] plainFrame =
            PerformanceProtocol.Serialize(
                Packet);

        byte[] protectedFrame =
            PerformanceProtocol.Serialize(
                Packet,
                protector);

        ValidateControlFrames(
            plainFrame,
            protectedFrame,
            protector);

        WarmUp(
            plainFrame,
            protectedFrame,
            protector);

        MeasurementResult plainSerialize =
            MeasurePlainSerialize(
                PlainMeasurementIterations);

        MeasurementResult plainDeserialize =
            MeasurePlainDeserialize(
                plainFrame,
                PlainMeasurementIterations);

        MeasurementResult protectedSerialize =
            MeasureProtectedSerialize(
                protector,
                ProtectedMeasurementIterations);

        MeasurementResult protectedDeserialize =
            MeasureProtectedDeserialize(
                protectedFrame,
                protector,
                ProtectedMeasurementIterations);

        WriteEnvironment(
            plainFrame.Length,
            protectedFrame.Length);

        WriteMeasurement(
            plainSerialize);

        WriteMeasurement(
            plainDeserialize);

        WriteMeasurement(
            protectedSerialize);

        WriteMeasurement(
            protectedDeserialize);

        return 0;
    }

    /// <summary>
    /// Validates that plain and protected frames match expected byte lengths and round-trip successfully before benchmarking begins.
    /// </summary>
    /// <param name="plainFrame">The serialized plain frame bytes.</param>
    /// <param name="protectedFrame">The serialized protected frame bytes.</param>
    /// <param name="protector">The cryptographic payload protector used for encryption and decryption.</param>
    private static void ValidateControlFrames(
        byte[] plainFrame,
        byte[] protectedFrame,
        AesGcmPayloadProtector protector)
    {
        if (plainFrame.Length != 19)
        {
            throw new InvalidOperationException(
                $"Expected a 19-byte plain frame, but received {plainFrame.Length} bytes.");
        }

        if (protectedFrame.Length != 47)
        {
            throw new InvalidOperationException(
                $"Expected a 47-byte protected frame, but received {protectedFrame.Length} bytes.");
        }

        PerformancePacket plain =
            PerformanceProtocol.Deserialize<PerformancePacket>(
                plainFrame);

        PerformancePacket protectedPacket =
            PerformanceProtocol.Deserialize<PerformancePacket>(
                protectedFrame,
                protector);

        ValidatePacket(
            plain);

        ValidatePacket(
            protectedPacket);
    }

    /// <summary>
    /// Validates that a deserialized packet contains identical field values to the baseline packet.
    /// </summary>
    /// <param name="packet">The packet instance to validate.</param>
    private static void ValidatePacket(
        PerformancePacket packet)
    {
        if (packet.Number != 0x11223344 ||
            packet.Sequence != 0x0102030405060708 ||
            !packet.Enabled)
        {
            throw new InvalidOperationException(
                "The performance control packet did not round-trip correctly.");
        }
    }

    /// <summary>
    /// Executes warmup iterations to ensure code paths are JIT-compiled before measuring latency and allocations.
    /// </summary>
    /// <param name="plainFrame">The serialized plain frame bytes.</param>
    /// <param name="protectedFrame">The serialized protected frame bytes.</param>
    /// <param name="protector">The payload protector instance.</param>
    private static void WarmUp(
        byte[] plainFrame,
        byte[] protectedFrame,
        AesGcmPayloadProtector protector)
    {
        long checksum = 0;

        for (
            int index = 0;
            index < WarmupIterations;
            index++)
        {
            byte[] serialized =
                PerformanceProtocol.Serialize(
                    Packet);

            checksum +=
                serialized.Length;

            PerformancePacket plain =
                PerformanceProtocol.Deserialize<PerformancePacket>(
                    plainFrame);

            checksum +=
                plain.Number;

            byte[] protectedSerialized =
                PerformanceProtocol.Serialize(
                    Packet,
                    protector);

            checksum +=
                protectedSerialized.Length;

            PerformancePacket protectedPacket =
                PerformanceProtocol.Deserialize<PerformancePacket>(
                    protectedFrame,
                    protector);

            checksum +=
                protectedPacket.Number;
        }

        if (checksum == long.MinValue)
        {
            throw new InvalidOperationException(
                "Warm-up checksum reached an impossible sentinel value.");
        }
    }

    /// <summary>
    /// Measures execution duration and memory allocations for serializing plain packets.
    /// </summary>
    /// <param name="iterations">The number of serialization iterations to perform.</param>
    /// <returns>The aggregated measurement results.</returns>
    private static MeasurementResult MeasurePlainSerialize(
        int iterations)
    {
        long checksum = 0;

        long allocatedBefore =
            GC.GetAllocatedBytesForCurrentThread();

        long timestampBefore =
            Stopwatch.GetTimestamp();

        for (
            int index = 0;
            index < iterations;
            index++)
        {
            byte[] frame =
                PerformanceProtocol.Serialize(
                    Packet);

            checksum +=
                frame.Length;

            checksum +=
                frame[0];
        }

        long timestampAfter =
            Stopwatch.GetTimestamp();

        long allocatedAfter =
            GC.GetAllocatedBytesForCurrentThread();

        return CreateMeasurement(
            "plain.serialize",
            iterations,
            allocatedAfter -
                allocatedBefore,
            timestampAfter -
                timestampBefore,
            checksum);
    }

    /// <summary>
    /// Measures execution duration and memory allocations for deserializing plain frames.
    /// </summary>
    /// <param name="frame">The serialized frame bytes to deserialize.</param>
    /// <param name="iterations">The number of deserialization iterations to perform.</param>
    /// <returns>The aggregated measurement results.</returns>
    private static MeasurementResult MeasurePlainDeserialize(
        byte[] frame,
        int iterations)
    {
        long checksum = 0;

        long allocatedBefore =
            GC.GetAllocatedBytesForCurrentThread();

        long timestampBefore =
            Stopwatch.GetTimestamp();

        for (
            int index = 0;
            index < iterations;
            index++)
        {
            PerformancePacket packet =
                PerformanceProtocol.Deserialize<PerformancePacket>(
                    frame);

            checksum +=
                packet.Number;

            checksum +=
                packet.Enabled
                    ? 1
                    : 0;
        }

        long timestampAfter =
            Stopwatch.GetTimestamp();

        long allocatedAfter =
            GC.GetAllocatedBytesForCurrentThread();

        return CreateMeasurement(
            "plain.deserialize",
            iterations,
            allocatedAfter -
                allocatedBefore,
            timestampAfter -
                timestampBefore,
            checksum);
    }

    /// <summary>
    /// Measures execution duration and memory allocations for serializing and encrypting packets using AES-GCM.
    /// </summary>
    /// <param name="protector">The payload protector instance.</param>
    /// <param name="iterations">The number of iterations to perform.</param>
    /// <returns>The aggregated measurement results.</returns>
    private static MeasurementResult MeasureProtectedSerialize(
        AesGcmPayloadProtector protector,
        int iterations)
    {
        long checksum = 0;

        long allocatedBefore =
            GC.GetAllocatedBytesForCurrentThread();

        long timestampBefore =
            Stopwatch.GetTimestamp();

        for (
            int index = 0;
            index < iterations;
            index++)
        {
            byte[] frame =
                PerformanceProtocol.Serialize(
                    Packet,
                    protector);

            checksum +=
                frame.Length;

            checksum +=
                frame[0];
        }

        long timestampAfter =
            Stopwatch.GetTimestamp();

        long allocatedAfter =
            GC.GetAllocatedBytesForCurrentThread();

        return CreateMeasurement(
            "protected.serialize.aesgcm",
            iterations,
            allocatedAfter -
                allocatedBefore,
            timestampAfter -
                timestampBefore,
            checksum);
    }

    /// <summary>
    /// Measures execution duration and memory allocations for decrypting and deserializing frames using AES-GCM.
    /// </summary>
    /// <param name="frame">The protected frame bytes to decrypt and deserialize.</param>
    /// <param name="protector">The payload protector instance.</param>
    /// <param name="iterations">The number of iterations to perform.</param>
    /// <returns>The aggregated measurement results.</returns>
    private static MeasurementResult MeasureProtectedDeserialize(
        byte[] frame,
        AesGcmPayloadProtector protector,
        int iterations)
    {
        long checksum = 0;

        long allocatedBefore =
            GC.GetAllocatedBytesForCurrentThread();

        long timestampBefore =
            Stopwatch.GetTimestamp();

        for (
            int index = 0;
            index < iterations;
            index++)
        {
            PerformancePacket packet =
                PerformanceProtocol.Deserialize<PerformancePacket>(
                    frame,
                    protector);

            checksum +=
                packet.Number;

            checksum +=
                packet.Enabled
                    ? 1
                    : 0;
        }

        long timestampAfter =
            Stopwatch.GetTimestamp();

        long allocatedAfter =
            GC.GetAllocatedBytesForCurrentThread();

        return CreateMeasurement(
            "protected.deserialize.aesgcm",
            iterations,
            allocatedAfter -
                allocatedBefore,
            timestampAfter -
                timestampBefore,
            checksum);
    }

    /// <summary>
    /// Calculates per-operation latency and memory allocation metrics from raw benchmark execution counters.
    /// </summary>
    /// <param name="name">The measurement scenario name.</param>
    /// <param name="iterations">The count of executed iterations.</param>
    /// <param name="allocatedBytes">The total bytes allocated during the measurement.</param>
    /// <param name="elapsedTimestampTicks">The elapsed ticks measured by <see cref="Stopwatch"/>.</param>
    /// <param name="checksum">The verification checksum computed during iterations.</param>
    /// <returns>A populated <see cref="MeasurementResult"/> instance.</returns>
    private static MeasurementResult CreateMeasurement(
        string name,
        int iterations,
        long allocatedBytes,
        long elapsedTimestampTicks,
        long checksum)
    {
        double allocatedBytesPerOperation =
            (double)allocatedBytes /
            iterations;

        double nanosecondsPerOperation =
            elapsedTimestampTicks *
            (1_000_000_000d /
             Stopwatch.Frequency) /
            iterations;

        return new MeasurementResult(
            name,
            iterations,
            allocatedBytes,
            allocatedBytesPerOperation,
            nanosecondsPerOperation,
            checksum);
    }

    /// <summary>
    /// Writes host environment and benchmark configuration metadata to standard output.
    /// </summary>
    /// <param name="plainFrameLength">The byte length of the plain test frame.</param>
    /// <param name="protectedFrameLength">The byte length of the protected test frame.</param>
    private static void WriteEnvironment(
        int plainFrameLength,
        int protectedFrameLength)
    {
        Console.WriteLine(
            "PACKETWIRE_G6A_BASELINE");

        Console.WriteLine(
            "FRAMEWORK|" +
            RuntimeInformation.FrameworkDescription);

        Console.WriteLine(
            "RUNTIME_VERSION|" +
            Environment.Version);

        Console.WriteLine(
            "OS|" +
            RuntimeInformation.OSDescription);

        Console.WriteLine(
            "PROCESS_ARCHITECTURE|" +
            RuntimeInformation.ProcessArchitecture);

        Console.WriteLine(
            "PROCESSOR_COUNT|" +
            Environment.ProcessorCount.ToString(
                CultureInfo.InvariantCulture));

        Console.WriteLine(
            "SERVER_GC|" +
            (GCSettings.IsServerGC
                ? "true"
                : "false"));

        Console.WriteLine(
            "STOPWATCH_FREQUENCY|" +
            Stopwatch.Frequency.ToString(
                CultureInfo.InvariantCulture));

        Console.WriteLine(
            "WARMUP_ITERATIONS|" +
            WarmupIterations.ToString(
                CultureInfo.InvariantCulture));

        Console.WriteLine(
            "PLAIN_FRAME_LENGTH|" +
            plainFrameLength.ToString(
                CultureInfo.InvariantCulture));

        Console.WriteLine(
            "PROTECTED_FRAME_LENGTH|" +
            protectedFrameLength.ToString(
                CultureInfo.InvariantCulture));

        Console.WriteLine(
            "SCENARIO|ITERATIONS|TOTAL_ALLOCATED_BYTES|ALLOCATED_BYTES_PER_OP|NANOSECONDS_PER_OP|CHECKSUM");
    }

    /// <summary>
    /// Writes formatted measurement metrics for a single benchmark scenario to standard output.
    /// </summary>
    /// <param name="result">The measurement result structure to print.</param>
    private static void WriteMeasurement(
        MeasurementResult result)
    {
        string line =
            result.Name +
            "|" +
            result.Iterations.ToString(
                CultureInfo.InvariantCulture) +
            "|" +
            result.TotalAllocatedBytes.ToString(
                CultureInfo.InvariantCulture) +
            "|" +
            result.AllocatedBytesPerOperation.ToString(
                "F4",
                CultureInfo.InvariantCulture) +
            "|" +
            result.NanosecondsPerOperation.ToString(
                "F4",
                CultureInfo.InvariantCulture) +
            "|" +
            result.Checksum.ToString(
                CultureInfo.InvariantCulture);

        Console.WriteLine(
            line);
    }

    /// <summary>
    /// Contains calculated metrics for a benchmark measurement scenario.
    /// </summary>
    /// <param name="Name">The scenario name.</param>
    /// <param name="Iterations">The number of executed iterations.</param>
    /// <param name="TotalAllocatedBytes">The total bytes allocated across all iterations.</param>
    /// <param name="AllocatedBytesPerOperation">The average bytes allocated per operation.</param>
    /// <param name="NanosecondsPerOperation">The average execution time in nanoseconds per operation.</param>
    /// <param name="Checksum">A checksum accumulating operation outputs to prevent dead code elimination.</param>
    private readonly record struct MeasurementResult(
        string Name,
        int Iterations,
        long TotalAllocatedBytes,
        double AllocatedBytesPerOperation,
        double NanosecondsPerOperation,
        long Checksum);
}