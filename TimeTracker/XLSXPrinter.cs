using System;
using System.Collections.Generic;
using System.Text;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Spreadsheet;

namespace TimeTracker
{
    public class XLSXPrinter
    {
        public static void Export(string path, List<TimeInfo> infos)
        {
            var file = new XLWorkbook();
            var sh = file.Worksheets.Add("Times");

            sh.Column(1).Width = 20;
            sh.Column(2).Width = 20;
            sh.Column(3).Width = 20;

            sh.Range(1, 1, 1, 3).Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
            sh.Range(1, 1, 1, 3).Style.Border.InsideBorder = XLBorderStyleValues.Medium;

            sh.Cell(1, 1).Value = "Start";
            sh.Cell(1, 2).Value = "End";
            sh.Cell(1, 3).Value = "Work Time";

            TimeSpan count = new();

            for (int i = 2; i < infos.Count + 2; i++)
            {
                sh.Cell(i, 1).Value = infos[i - 2].Start.ToString("MM.dd HH:mm");
                sh.Cell(i, 2).Value = infos[i - 2].End.ToString("MM.dd HH:mm");
                sh.Cell(i, 3).Value = infos[i - 2].TimeWorked.ToString(@"hh\:mm\:ss");
                count += infos[i - 2].TimeWorked;
            }

            int lastRow = infos.Count + 1;

            sh.Range(2, 1, lastRow, 3).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            sh.Cell(lastRow + 1, 1).Value = "Work time count";
            sh.Cell(lastRow + 1, 2).Value = Math.Round(count.TotalHours, 2);

            sh.Range(lastRow + 1, 1, lastRow + 1, 2).Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
            sh.Range(lastRow + 1, 1, lastRow + 1, 2).Style.Border.InsideBorder = XLBorderStyleValues.Medium;


            file.SaveAs(path);
        }

    }
}
