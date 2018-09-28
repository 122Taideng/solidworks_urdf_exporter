﻿using log4net;
using Microsoft.VisualBasic.FileIO;
using SW2URDF.URDF;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace SW2URDF.CSV
{
    /// <summary>
    /// Class to perform exporting to CSV and eventually importing of data from a CSV file
    /// </summary>
    public static class ImportExport
    {
        private static readonly ILog logger = Logger.GetLogger();

        #region Public Methods

        /// <summary>
        /// Method to write a full URDF robot to a CSV
        /// </summary>
        /// <param name="robot">URDF robot tree</param>
        /// <param name="filename">Fully qualified string name to write to</param>
        public static void WriteRobotToCSV(Robot robot, string filename)
        {
            logger.Info("Writing CSV file " + filename);
            using (StreamWriter stream = new StreamWriter(filename))
            {
                WriteHeaderToCSV(stream);
                WriteLinkToCSV(stream, robot.BaseLink);
            }
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Iterates through the column names and writes them to a file stream
        /// </summary>
        /// <param name="stream">Stream to write to</param>
        private static void WriteHeaderToCSV(StreamWriter stream)
        {
            StringBuilder builder = new StringBuilder();
            foreach (DictionaryEntry entry in ContextToColumns.Dictionary)
            {
                string context = (string)entry.Key;
                string column = (string)entry.Value;

                builder = builder.Append(column).Append(",");
            }
            stream.WriteLine(builder.ToString() + "\n");
        }

        /// <summary>
        /// Appends a line to an open CSV document of a URDF's Link properties
        /// </summary>
        /// <param name="stream">Stream representing opened CSV file</param>
        /// <param name="dictionary">Dictionary of values</param>
        private static void WriteValuesToCSV(StreamWriter stream, OrderedDictionary dictionary)
        {
            StringBuilder builder = new StringBuilder();
            foreach (DictionaryEntry entry in ContextToColumns.Dictionary)
            {
                string context = (string)entry.Key;
                string column = (string)entry.Value;
                if (dictionary.Contains(context))
                {
                    object value = dictionary[context];
                    builder = builder.Append(value).Append(",");
                }
                else
                {
                    builder = builder.Append("").Append(",");
                }
            }

            HashSet<string> keys1 = new HashSet<string>(ContextToColumns.Dictionary.Keys.Cast<string>());
            HashSet<string> keys2 = new HashSet<string>(dictionary.Keys.Cast<string>());

            StringBuilder missingColumns = new StringBuilder();
            foreach (string missing in keys2.Except(keys1))
            {
                missingColumns.Append(missing).Append(",");
            }
            if (missingColumns.Length > 0)
            {
                logger.Error("The following columns were not written to the CSV: " + missingColumns.ToString());
            }

            stream.WriteLine(builder.ToString() + "\n");
        }

        /// <summary>
        /// Converts a URDF Link to a dictionary of values and writes them to a CSV
        /// </summary>
        /// <param name="stream">StreamWriter of opened CSV document</param>
        /// <param name="link">URDF link to append to the file</param>
        private static void WriteLinkToCSV(StreamWriter stream, Link link)
        {
            OrderedDictionary dictionary = new OrderedDictionary();
            link.AppendToCSVDictionary(new List<string>(), dictionary);
            WriteValuesToCSV(stream, dictionary);

            foreach (Link child in link.Children)
            {
                WriteLinkToCSV(stream, child);
            }
        }

        public static Link LoadURDFRobotFromCSV(Stream stream)
        {
            List<StringDictionary> loadedFields = new List<StringDictionary>();
            using (TextFieldParser csvParser = new TextFieldParser(stream))
            {
                csvParser.SetDelimiters(new string[] { "," });

                string[] headers = csvParser.ReadFields();
                while (!csvParser.EndOfData)
                {
                    string[] fields = csvParser.ReadFields();
                    StringDictionary dictionary = new StringDictionary();
                    for (int i = 0; i < fields.Length; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(fields[i]))
                        {
                            dictionary[headers[i]] = fields[i];
                        }
                    }
                    loadedFields.Add(dictionary);
                }

                return BuildURDFRobotFromData(loadedFields);
            }
        }

        public static List<StringDictionary> FindOrphanLinks(List<StringDictionary> allLinks, List<StringDictionary> linksWithParents)
        {
            HashSet<string> linkNames = new HashSet<string>(
                allLinks.Select(dictionary =>
                    dictionary[ContextToColumns.KEY_NAME]));

            List<StringDictionary> orphanLinks =
                linksWithParents.Where(dictionary =>
                    !linkNames.Contains(dictionary[ContextToColumns.KEY_PARENT_LINK])).ToList();

            return orphanLinks;
        }

        public static bool ValidateTree(List<StringDictionary> linksWithoutParents, List<StringDictionary> linksWithParents)
        {
            if (linksWithoutParents.Count == 0)
            {
                string message = "CSV Import failed. No base link was found. One link should have an empty parent field";
                MessageBox.Show(message);
                logger.Error(message);
                return false;
            }

            if (linksWithoutParents.Count > 1)
            {
                IEnumerable<string> orphanNames = linksWithoutParents.Select(d => d[ContextToColumns.KEY_NAME]);
                string names = string.Join(", ", orphanNames);

                string message = "CSV Import failed. The following links did not contain parent links, only one base link should exist\r\n" + names;
                MessageBox.Show(message);
                logger.Error(message);
                return false;
            }

            List<StringDictionary> allLinks = linksWithParents.Concat(linksWithoutParents).ToList();
            List<StringDictionary> orphaned = FindOrphanLinks(allLinks, linksWithParents);

            if (orphaned.Count > 0)
            {
                IEnumerable<string> orphanNames = orphaned.Select(d => d[ContextToColumns.KEY_NAME]);
                string names = string.Join(", ", orphanNames);

                string message = "CSV Import failed. The following links contained parent links that did not exist\r\n" + names;
                MessageBox.Show(message);
                logger.Error(message);
                return false;
            }

            return true;
        }

        public static Link BuildURDFRobotFromData(List<StringDictionary> loadedFields)
        {
            Link baseLink;
            List<StringDictionary> linksWithoutParents =
                loadedFields.Where(dictionary => !dictionary.ContainsKey(ContextToColumns.KEY_PARENT_LINK)).ToList();
            List<StringDictionary> linksWithParents =
                loadedFields.Where(dictionary => dictionary.ContainsKey(ContextToColumns.KEY_PARENT_LINK)).ToList();

            if (!ValidateTree(linksWithoutParents, linksWithParents))
            {
                return null;
            }

            baseLink = BuildLinkFromData(linksWithoutParents[0]);
            AddLinksToParent(baseLink, linksWithParents);
            return baseLink;
        }

        public static Link BuildLinkFromData(StringDictionary dictionary)
        {
            StringDictionary contextDictionary = new StringDictionary();
            foreach (DictionaryEntry entry in ContextToColumns.Dictionary)
            {
                string context = (string)entry.Key;
                string columnName = (string)entry.Value;

                if (dictionary.ContainsKey(columnName))
                {
                    contextDictionary[context] = dictionary[columnName];
                }
            }

            Link link = new Link();
            link.SetElementFromData(new List<string>(), contextDictionary);
            return link;
        }

        public static void AddLinksToParent(Link parent, List<StringDictionary> loadedFields)
        {
            IEnumerable<StringDictionary> children = loadedFields.Where(dictionary =>
                dictionary[ContextToColumns.KEY_PARENT_LINK] == parent.Name);

            foreach (StringDictionary dictionary in children)
            {
                Link child = BuildLinkFromData(dictionary);
                child.Parent = parent;
                AddLinksToParent(child, loadedFields);
                parent.Children.Add(child);
            }
        }

        #endregion Private Methods
    }
}