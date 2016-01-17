using Karan.Utilities.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Karan.IR.Lucene.Engine
{
    public class LuceneSearchEO
    {
        [LuceneType(true, 0, true)]
        public Guid Id { get; set; }
        [LuceneType(true, 0, false)]
        public string Title { get; set; }
        [LuceneType(true, 0, false)]
        public string Content { get; set; }
    }
}
