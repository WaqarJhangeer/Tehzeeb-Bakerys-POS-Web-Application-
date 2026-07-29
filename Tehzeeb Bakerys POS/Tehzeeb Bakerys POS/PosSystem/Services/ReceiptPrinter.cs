using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using PosSystem.Models;

namespace PosSystem.Services;


internal sealed class ReceiptPrinter
{
    private readonly Queue<PrintJob> _spool = new Queue<PrintJob>();
    private readonly string _outputFolder;

    internal ReceiptPrinter()
    {
        _outputFolder = Path.Combine(Directory.GetCurrentDirectory(), "receipts");
    }

    internal int PendingJobs => _spool.Count;

    internal string OutputFolder => _outputFolder;

    /// Adds a finished sale to the back of the print queue.
    internal void Enqueue(Order order)
    {
        if (order is null)
        {
            return;
        }

        _spool.Enqueue(new PrintJob(order.OrderNumber, order.BuildReceipt()));
    }

    /// Peeks at the job that will print next without removing it.
    internal bool TryPeekNext(out string orderNumber)
    {
        orderNumber = string.Empty;
        if (_spool.Count == 0)
        {
            return false;
        }

        orderNumber = _spool.Peek().OrderNumber;
        return true;
    }

   
    /// Task / async: drains the spool in FIFO order, writing one .txt file per receipt.
   
    internal async Task<int> FlushAsync()
    {
        if (_spool.Count == 0)
        {
            return 0;
        }

        Directory.CreateDirectory(_outputFolder);
        int printed = 0;

        while (_spool.Count > 0)
        {
            PrintJob job = _spool.Dequeue();

            // Simulate the printer chugging through the job.
            await Task.Delay(300);

            string path = Path.Combine(_outputFolder, job.OrderNumber + ".txt");
            try
            {
                using (StreamWriter writer = new StreamWriter(path, false))
                {
                    await writer.WriteAsync(job.Content);
                }

                Console.WriteLine($"  Printed {job.OrderNumber}  ->  {path}");
                printed++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Print failed for {job.OrderNumber}: {ex.Message}");
            }
        }

        return printed;
    }

    /// <summary>Private nested class - only the printer needs to know what a job looks like.</summary>
    private sealed class PrintJob
    {
        internal PrintJob(string orderNumber, string content)
        {
            OrderNumber = orderNumber;
            Content = content;
            QueuedAt = DateTime.Now;
        }

        internal string OrderNumber { get; }

        internal string Content { get; }

        internal DateTime QueuedAt { get; }
    }
}
