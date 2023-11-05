﻿using RawNet.Extensions;
using RawNet.Printer.Safe;

namespace RawNet.Printer;

public sealed class PrinterAdapter : IPrinterAdapter
{
    private static bool IsXPSDriver(SafePrinter printer)
    {
        var files = printer.GetPrinterDriverDependentFiles();

        return files.Any(f => f.EndsWith("pipelineconfig.xml", StringComparison.InvariantCultureIgnoreCase));
    }

    private void DocPrinter(SafePrinter printer, string documentName, string dataType, Stream stream, bool paused, int pagecount, string printerName)
    {
        var di1 = new DOC_INFO_1
        {
            pDataType = dataType,
            pDocName = documentName,
        };

        var id = printer.StartDocPrinter(di1);

        if (paused)
            NativeUtils.SetJob(printer.DangerousGetHandle(), id, 0, IntPtr.Zero, (int)JobControl.Pause);

        OnJobCreated?.Invoke(this, new JobCreatedEventArgs { Id = id, PrinterName = printerName });

        try
        {
            PagePrinter(printer, stream, pagecount);
        }
        finally
        {
            printer.EndDocPrinter();
        }
    }

    private static void PagePrinter(SafePrinter printer, Stream stream, int pagecount)
    {
        printer.StartPagePrinter();

        try
        {
            WritePrinter(printer, stream);
        }
        finally
        {
            printer.EndPagePrinter();
        }

        for (int i = 1; i < pagecount; i++)
        {
            printer.StartPagePrinter();
            printer.EndPagePrinter();
        }
    }

    private static void WritePrinter(SafePrinter printer, Stream stream)
    {
        stream.Seek(0, SeekOrigin.Begin);

        const int bufferSize = 1048576;
        var buffer = new byte[bufferSize];

        int read;
        while ((read = stream.Read(buffer, 0, bufferSize)) != 0)
            printer.WritePrinter(buffer, read);
    }

    public event EventHandler<JobCreatedEventArgs> OnJobCreated = null!;

    public void PrintRawFile(string printer, string path, bool paused = false)
    {
        PrintRawFile(printer, path, path, paused);
    }

    public void PrintRawFile(string printer, string path, string documentName, bool paused = false)
    {
        using var stream = File.OpenRead(path);
        PrintRawStream(printer, stream, documentName, paused);
    }

    public void PrintRawStream(string printer, Stream stream, string documentName, bool paused = false)
    {
        PrintRawStream(printer, stream, documentName, paused, 1);
    }

    public void PrintRawStream(string printer, Stream stream, string documentName, bool paused, int pagecount)
    {
        var defaults = new PRINTER_DEFAULTS
        {
            DesiredPrinterAccess = PRINTER_ACCESS_MASK.PRINTER_ACCESS_USE
        };

        using var safePrinter = SafePrinter.OpenPrinter(printer, ref defaults);
        DocPrinter(safePrinter, documentName, IsXPSDriver(safePrinter) ? "XPS_PASS" : "RAW", stream, paused, pagecount, printer);
    }
}