using System.IO;
using Bing.Offices.Excels.Abstractions;
using Bing.Offices.Excels.Core;

namespace Bing.Offices.Excels.Npoi.Core
{
    /// <summary>
    /// 基于Npoi的工作簿
    /// </summary>
    public class Workbook:WorkbookBase
    {
        public override IWorkSheet CreateSheet()
        {
            throw new System.NotImplementedException();
        }

        public override IWorkSheet GetOrCreateSheet()
        {
            throw new System.NotImplementedException();
        }

        public override IWorkSheet CreateSheet(string sheetName)
        {
            throw new System.NotImplementedException();
        }

        public override IWorkSheet GetOrCreateSheet(string sheetName)
        {
            throw new System.NotImplementedException();
        }

        public override IWorkSheet CloneSheet(int sheetIndex)
        {
            throw new System.NotImplementedException();
        }

        public override IWorkSheet CloneSheet(string sheetName)
        {
            throw new System.NotImplementedException();
        }

        public override void SaveToFile(string fileName)
        {
            throw new System.NotImplementedException();
        }

        public override void SaveToStream(Stream stream)
        {
            throw new System.NotImplementedException();
        }

        public override byte[] SaveToBuffer()
        {
            throw new System.NotImplementedException();
        }
    }
}
