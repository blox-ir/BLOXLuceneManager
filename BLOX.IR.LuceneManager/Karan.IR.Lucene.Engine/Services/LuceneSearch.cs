using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Version = Lucene.Net.Util.Version;
using System.Reflection;
using Karan.IR.EntityObject;
using Karan.IR.Lucene.Engine;

namespace Karan.IR.Lucene.Engine
{
    public static class LuceneSearchService
    {
        #region Search Methods
        public static IEnumerable<LuceneSearchEO> GetAllIndexRecords(LuceneSearchType searchType)
        {
            string _luceneDir = getLuceneDirectory(searchType);
            if (!System.IO.Directory.EnumerateFiles(_luceneDir).Any()) return new List<LuceneSearchEO>();

            // set up lucene searcher
            var searcher = new IndexSearcher(getFSDirectory(searchType), false);
            var reader = IndexReader.Open(getFSDirectory(searchType), false);
            var docs = new List<Document>();
            var term = reader.TermDocs();
            // v 2.9.4: use 'term.Doc()'
            // v 3.0.3: use 'term.Doc'
            while (term.Next()) docs.Add(searcher.Doc(term.Doc));
            reader.Dispose();
            searcher.Dispose();
            return _mapLuceneToDataList(docs);
        }
        public static IEnumerable<LuceneSearchEO> Search(LuceneSearchType searchType, string input, string fieldName = "")
        {
            if (string.IsNullOrEmpty(input)) return new List<LuceneSearchEO>();

            string[] temrs = input.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
            input = string.Empty;
            foreach (var item in temrs)
                input += "+" + item + " ";
            return _search(searchType, input.Trim(), fieldName);
        }
        #endregion

        #region Private Methods
        private static IEnumerable<LuceneSearchEO> _search(LuceneSearchType searchType, string searchQuery, string searchField = "")
        {
            // validation
            if (string.IsNullOrEmpty(searchQuery.Replace("*", "").Replace("?", ""))) return new List<LuceneSearchEO>();

            // set up lucene searcher
            using (var searcher = new IndexSearcher(getFSDirectory(searchType), false))
            {
                var hits_limit = 20;
                var analyzer = new StandardAnalyzer(Version.LUCENE_30);

                // search by single field
                if (!string.IsNullOrEmpty(searchField))
                {
                    var parser = new QueryParser(Version.LUCENE_30, searchField, analyzer);
                    var query = parseQuery(searchQuery, parser);
                    var hits = searcher.Search(query, hits_limit).ScoreDocs;
                    var results = _mapLuceneToDataList(hits, searcher);
                    analyzer.Close();
                    searcher.Dispose();
                    return results;
                }
                // search by multiple fields (ordered by RELEVANCE)
                else
                {
                    string[] fields = getLuceneSearchEOFields();
                    var parser = new MultiFieldQueryParser
                        (Version.LUCENE_30, fields, analyzer);
                    var query = parseQuery(searchQuery, parser);
                    var hits = searcher.Search(query, null, hits_limit, Sort.INDEXORDER).ScoreDocs;
                    var results = _mapLuceneToDataList(hits, searcher);
                    analyzer.Close();
                    searcher.Dispose();
                    return results;
                }
            }
        }
        private static Query parseQuery(string searchQuery, QueryParser parser)
        {
            Query query;
            try
            {
                query = parser.Parse(searchQuery.Trim());
            }
            catch (ParseException)
            {
                query = parser.Parse(QueryParser.Escape(searchQuery.Trim()));
            }
            return query;
        }
        // map Lucene search index to data
        private static IEnumerable<LuceneSearchEO> _mapLuceneToDataList(IEnumerable<Document> hits)
        {
            return hits.Select(_mapLuceneDocumentToData).ToList();
        }
        private static IEnumerable<LuceneSearchEO> _mapLuceneToDataList(IEnumerable<ScoreDoc> hits, IndexSearcher searcher)
        {
            // v 2.9.4: use 'hit.doc'
            // v 3.0.3: use 'hit.Doc'
            return hits.Select(hit => _mapLuceneDocumentToData(searcher.Doc(hit.Doc))).ToList();
        }
        private static string getLuceneDirectory(LuceneSearchType searchType)
        {
            string lucenePath = HttpContext.Current.Request.PhysicalApplicationPath + "Lucene_Index\\";
            DirectoryInfo diLucenePath = new DirectoryInfo(lucenePath);
            if (!diLucenePath.Exists)
                diLucenePath.Create();
            string _luceneDir = Path.Combine(lucenePath + searchType.ToString() + "_index");
            diLucenePath = new DirectoryInfo(_luceneDir);
            if (!diLucenePath.Exists)
                diLucenePath.Create();
            return _luceneDir;
        }
        #endregion

        #region Add Update Clear Search Index Data
        public static void AddUpdateLuceneIndex(LuceneSearchType searchType, LuceneSearchEO LuceneSearchEO)
        {
            AddUpdateLuceneIndex(searchType, new List<LuceneSearchEO> { LuceneSearchEO });
        }
        public static void AddUpdateLuceneIndex(LuceneSearchType searchType, IEnumerable<LuceneSearchEO> LuceneSearchEOs)
        {
            string _luceneDir = getLuceneDirectory(searchType);
            // init lucene
            var analyzer = new StandardAnalyzer(Version.LUCENE_30);
            using (var writer = new IndexWriter(getFSDirectory(searchType), analyzer, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                // add data to lucene search index (replaces older entries if any)
                foreach (var LuceneSearchEO in LuceneSearchEOs) _addToLuceneIndex(LuceneSearchEO, writer);

                // close handles
                analyzer.Close();
                writer.Dispose();
            }
        }
        private static FSDirectory getFSDirectory(LuceneSearchType searchType)
        {
            string _luceneDir = getLuceneDirectory(searchType);
            FSDirectory _directoryTemp = FSDirectory.Open(new DirectoryInfo(_luceneDir));
            if (IndexWriter.IsLocked(_directoryTemp)) IndexWriter.Unlock(_directoryTemp);
            var lockFilePath = Path.Combine(_luceneDir, "write.lock");
            if (File.Exists(lockFilePath)) File.Delete(lockFilePath);
            return _directoryTemp;
        }
        public static void ClearLuceneIndexRecord(LuceneSearchType searchType, int record_id)
        {
            // init lucene
            var analyzer = new StandardAnalyzer(Version.LUCENE_30);
            using (var writer = new IndexWriter(getFSDirectory(searchType), analyzer, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                LuceneSearchEO LuceneSearchEO = new LuceneSearchEO();
                PropertyInfo[] props = LuceneSearchEO.GetType().GetProperties();
                foreach (var prop in props)
                {
                    Attribute att = prop.GetCustomAttribute(typeof(LuceneTypeAttribute));
                    LuceneTypeAttribute luceneAtt = (LuceneTypeAttribute)att;
                    if (luceneAtt != null && luceneAtt.Key)
                    {
                        // remove older index entry
                        var searchQuery = new TermQuery(new Term(prop.Name, record_id.ToString()));
                        writer.DeleteDocuments(searchQuery);
                    }
                }
                // close handles
                analyzer.Close();
                writer.Dispose();
            }
        }
        public static bool ClearLuceneIndex(LuceneSearchType searchType)
        {
            try
            {
                var analyzer = new StandardAnalyzer(Version.LUCENE_30);
                using (var writer = new IndexWriter(getFSDirectory(searchType), analyzer, true, IndexWriter.MaxFieldLength.UNLIMITED))
                {
                    // remove older index entries
                    writer.DeleteAll();

                    // close handles
                    analyzer.Close();
                    writer.Dispose();
                }
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }
        public static void Optimize(LuceneSearchType searchType)
        {
            var analyzer = new StandardAnalyzer(Version.LUCENE_30);
            using (var writer = new IndexWriter(getFSDirectory(searchType), analyzer, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                analyzer.Close();
                writer.Optimize();
                writer.Dispose();
            }
        }
        #endregion

        #region Reflection Methods
        private static void _addToLuceneIndex(LuceneSearchEO LuceneSearchEO, IndexWriter writer)
        {
            PropertyInfo[] props = LuceneSearchEO.GetType().GetProperties();
            foreach (var prop in props)
            {
                Attribute att = prop.GetCustomAttribute(typeof(LuceneTypeAttribute));
                LuceneTypeAttribute luceneAtt = (LuceneTypeAttribute)att;
                if (luceneAtt != null && luceneAtt.Key)
                {
                    // remove older index entry
                    var searchQuery = new TermQuery(new Term(prop.Name, prop.GetValue(LuceneSearchEO).ToString()));
                    writer.DeleteDocuments(searchQuery);
                }
            }

            // add new index entry
            var doc = new Document();

            // add lucene fields mapped to db fields

            foreach (var prop in props)
            {
                Attribute att = prop.GetCustomAttribute(typeof(LuceneTypeAttribute));
                LuceneTypeAttribute luceneAtt = (LuceneTypeAttribute)att;
                if (luceneAtt != null)
                {
                    string value = string.Empty;
                    if (luceneAtt.Key)
                        doc.Add(new Field(prop.Name, prop.GetValue(LuceneSearchEO).ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED));
                    else
                    {
                        value = Convert.ToString(LuceneSearchEO.GetType().GetProperty(prop.Name).GetValue(LuceneSearchEO));
                        doc.Add(new Field(prop.Name, value, Field.Store.YES, Field.Index.ANALYZED));
                    }
                }
            }
            // add entry to index
            writer.AddDocument(doc);
        }
        private static LuceneSearchEO _mapLuceneDocumentToData(Document doc)
        {
            LuceneSearchEO obj = new LuceneSearchEO();
            PropertyInfo[] props = obj.GetType().GetProperties();

            foreach (var prop in props)
            {
                if (obj.GetType().GetProperty(prop.Name).PropertyType.FullName == "System.Guid")
                    obj.GetType().GetProperty(prop.Name).SetValue(obj, Guid.Parse(doc.Get(prop.Name)));
                else
                    obj.GetType().GetProperty(prop.Name).SetValue(obj, doc.Get(prop.Name));
            }
            return obj;
        }
        private static string[] getLuceneSearchEOFields()
        {
            LuceneSearchEO obj = new LuceneSearchEO();
            PropertyInfo[] props = obj.GetType().GetProperties();
            return props.Select(a => a.Name).ToArray();
        }
        #endregion
    }
}