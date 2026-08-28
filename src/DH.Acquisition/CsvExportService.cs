using System.IO;
using System.Text;

namespace DH.Acquisition;

/// <summary>
/// CSV 数据导出服务：将采集数据导出为 CSV 文件
/// </summary>
public static class CsvExportService
{
    /// <summary>
    /// 导出整个数据文件为 CSV
    /// </summary>
    public static bool ExportToCsv(string dataFilePath, string csvFilePath, int maxSamples = 0)
    {
        var info = DataStorageService.ReadHeader(dataFilePath);
        if (info == null)
            return false;

        var totalSamples = maxSamples > 0 ? Math.Min(maxSamples, info.TotalSamples) : info.TotalSamples;
        var channelCount = info.ChannelCount;

        using var writer = new StreamWriter(csvFilePath, false, Encoding.UTF8);
        var header = new StringBuilder("SampleIndex,Timestamp(s)");
        for (int ch = 0; ch < channelCount; ch++)
            header.Append($",CH{ch + 1}");
        writer.WriteLine(header.ToString());

        using var reader = new BinaryReader(new FileStream(dataFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
        reader.BaseStream.Seek(info.DataOffset, SeekOrigin.Begin);

        var batchSize = 8192;
        var samplesExported = 0L;
        var dt = 1.0 / info.SampleRate;

        while (samplesExported < totalSamples)
        {
            var toRead = (int)Math.Min(batchSize, totalSamples - samplesExported);
            var floatsToRead = toRead * channelCount;
            var buffer = new float[floatsToRead];

            for (int i = 0; i < floatsToRead; i++)
            {
                try { buffer[i] = reader.ReadSingle(); }
                catch { return false; }
            }

            var sb = new StringBuilder();
            for (int s = 0; s < toRead; s++)
            {
                var sampleIdx = samplesExported + s;
                sb.Append(sampleIdx);
                sb.Append(',');
                sb.Append((sampleIdx * dt).ToString("F6"));
                for (int ch = 0; ch < channelCount; ch++)
                {
                    sb.Append(',');
                    sb.Append(buffer[s * channelCount + ch].ToString("G"));
                }
                sb.AppendLine();
            }
            writer.Write(sb.ToString());
            samplesExported += toRead;
        }

        return true;
    }
}
