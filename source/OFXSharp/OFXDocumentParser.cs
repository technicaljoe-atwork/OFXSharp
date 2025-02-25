using Sgml;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace OFXSharp
{
    public class OfxDocumentParser
    {
        public OfxDocument Import(FileStream stream)
        {
            using (var reader = new StreamReader(stream, Encoding.Default))
            {
                return Import(reader.ReadToEnd());
            }
        }

        public OfxDocument Import(string ofx)
        {
            return ParseOfxDocument(ofx);
        }

        private OfxDocument ParseOfxDocument(string ofxString)
        {
            //If OFX file in SGML format, convert to XML
            if (!IsXmlVersion(ofxString))
            {
                ofxString = SgmlToXml(ofxString);
            }

            return Parse(ofxString);
        }

        /// <summary>
        /// Parse
        /// </summary>
        /// <param name="ofxString">OFX String</param>
        /// <returns>OFX Document</returns>
        private OfxDocument Parse(string ofxString)
        {
            var ofx = new OfxDocument { AccType = GetAccountType(ofxString) };

            using (var stringReader = new StringReader(ofxString))
            {
                var doc = new XmlDocument();
                doc.Load(stringReader);

                ofx.Currency = GetCurrency(doc, ofx.AccType);
                ofx.SignOn = GetSignOn(doc);
                ofx.Account = GetAccount(doc, ofx.AccType);
                ImportTransactions(ofx, doc);
                ofx.Balance = GetBalance(doc, ofx.AccType);
            }

            return ofx;
        }

        /// <summary>
        /// Get Currency
        /// </summary>
        /// <param name="doc">XML Document</param>
        /// <param name="accType">Account Type</param>
        /// <returns>Currency</returns>
        /// <exception cref="OfxParseException"></exception>
        private string GetCurrency(XmlDocument doc, AccountType accType)
        {
            var currencyNode = doc.SelectSingleNode(GetXPath(accType, OfxSection.CURRENCY));
            if (currencyNode != null)
            {
                return currencyNode.FirstChild.Value;
            }
            throw new OfxParseException("Currency not found");
        }

        /// <summary>
        /// Get Sign On
        /// </summary>
        /// <param name="doc">XML Document</param>
        /// <returns>Sign On</returns>
        /// <exception cref="OfxParseException"></exception>
        private SignOn GetSignOn(XmlDocument doc)
        {
            var signOnNode = doc.SelectSingleNode(Resources.SignOn);
            if (signOnNode != null)
            {
                return new SignOn(signOnNode);
            }
            throw new OfxParseException("Sign On information not found");
        }

        /// <summary>
        /// Get Account
        /// </summary>
        /// <param name="doc">XML Document</param>
        /// <param name="accType">Account Type</param>
        /// <returns>Account</returns>
        /// <exception cref="OfxParseException"></exception>
        private Account GetAccount(XmlDocument doc, AccountType accType)
        {
            var accountNode = doc.SelectSingleNode(GetXPath(accType, OfxSection.ACCOUNTINFO));
            if (accountNode != null)
            {
                return new Account(accountNode, accType);
            }
            throw new OfxParseException("Account information not found");
        }

        /// <summary>
        /// Get Balance
        /// </summary>
        /// <param name="doc">XML Document</param>
        /// <param name="accType">Account Type</param>
        /// <returns>Balance</returns>
        /// <exception cref="OfxParseException"></exception>
        private Balance GetBalance(XmlDocument doc, AccountType accType)
        {
            var ledgerNode = doc.SelectSingleNode(GetXPath(accType, OfxSection.BALANCE) + "/LEDGERBAL");
            var availableNode = doc.SelectSingleNode(GetXPath(accType, OfxSection.BALANCE) + "/AVAILBAL");

            if (ledgerNode != null) // && availableNode != null
            {
                return new Balance(ledgerNode, availableNode);
            }
            throw new OfxParseException("Balance information not found");
        }

        /// <summary>
        /// Returns the correct xpath to specified section for given account type
        /// </summary>
        /// <param name="type">Account type</param>
        /// <param name="section">Section of OFX document, e.g. Transaction Section</param>
        /// <exception cref="OfxException">Thrown in account type not supported</exception>
        private string GetXPath(AccountType type, OfxSection section)
        {
            string xpath, accountInfo;

            switch (type)
            {
                case AccountType.BANK:
                    xpath = Resources.BankAccount;
                    accountInfo = "/BANKACCTFROM";
                    break;
                case AccountType.CC:
                    xpath = Resources.CCAccount;
                    accountInfo = "/CCACCTFROM";
                    break;
                default:
                    throw new OfxException("Account Type not supported. Account type " + type);
            }

            switch (section)
            {
                case OfxSection.ACCOUNTINFO:
                    return xpath + accountInfo;
                case OfxSection.BALANCE:
                    return xpath;
                case OfxSection.TRANSACTIONS:
                    return xpath + "/BANKTRANLIST";
                case OfxSection.SIGNON:
                    return Resources.SignOn;
                case OfxSection.CURRENCY:
                    return xpath + "/CURDEF";
                default:
                    throw new OfxException("Unknown section found when retrieving XPath. Section " + section);
            }
        }

        /// <summary>
        /// Returns list of all transactions in OFX document
        /// </summary>
        /// <param name="ofxDocument">OFX document</param>
        /// <param name="doc">XML document</param>
        /// <returns>List of transactions found in OFX document</returns>
        private void ImportTransactions(OfxDocument ofxDocument, XmlDocument doc)
        {
            var xpath = GetXPath(ofxDocument.AccType, OfxSection.TRANSACTIONS);

            ofxDocument.StatementStart = doc.GetValue(xpath + "//DTSTART").ToDate();
            ofxDocument.StatementEnd = doc.GetValue(xpath + "//DTEND").ToDate();

            var transactionNodes = doc.SelectNodes(xpath + "//STMTTRN");

            ofxDocument.Transactions = new List<Transaction>();

            if (transactionNodes == null)
                return;

            foreach (XmlNode node in transactionNodes)
                ofxDocument.Transactions.Add(new Transaction(node, ofxDocument.Currency));
        }


        /// <summary>
        /// Checks account type of supplied file
        /// </summary>
        /// <param name="file">OFX file want to check</param>
        /// <returns>Account type for account supplied in ofx file</returns>
        private AccountType GetAccountType(string file)
        {
            if (file.IndexOf("<CREDITCARDMSGSRSV1>") != -1)
                return AccountType.CC;

            if (file.IndexOf("<BANKMSGSRSV1>") != -1)
                return AccountType.BANK;

            throw new OfxException("Unsupported Account Type");
        }

        /// <summary>
        /// Check if OFX file is in SGML or XML format
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        private bool IsXmlVersion(string file)
        {
            return (file.IndexOf("OFXHEADER:100") == -1);
        }

        /// <summary>
        /// Converts SGML to XML
        /// </summary>
        /// <param name="file">OFX File (SGML Format)</param>
        /// <returns>OFX File in XML format</returns>
        private string SgmlToXml(string file)
        {
            var reader = new SgmlReader
            {
                InputStream = new StringReader(ParseHeader(file)),
                DocType = "OFX"
            };

            var stringBuilder = new StringBuilder();
            using (var xmlWriter = new XmlTextWriter(new StringWriter(stringBuilder)))
            {
                while (!reader.EOF)
                {
                    xmlWriter.WriteNode(reader, true);
                }
            }

            var temp = stringBuilder.ToString().TrimStart().Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Join("", temp);
        }

        /// <summary>
        /// Checks that the file is supported by checking the header. Removes the header.
        /// </summary>
        /// <param name="file">OFX file</param>
        /// <returns>File, without the header</returns>
        private string ParseHeader(string file)
        {
            //Select header of file and split into array
            //End of header worked out by finding first instance of '<'
            //Array split based of new line & carriage return
            var header = file.Substring(0, file.IndexOf('<'))
               .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            //Check that no errors in header
            CheckHeader(header);

            //Remove header
            return file.Substring(file.IndexOf('<')).Trim();
        }

        /// <summary>
        /// Checks that all the elements in the header are supported
        /// </summary>
        /// <param name="header">Header of OFX file in array</param>
        private void CheckHeader(string[] header)
        {
            if (header[0] == "OFXHEADER:100DATA:OFXSGMLVERSION:102SECURITY:NONEENCODING:USASCIICHARSET:1252COMPRESSION:NONEOLDFILEUID:NONENEWFILEUID:NONE")//non delimited header
                return;
            if (header[0] != "OFXHEADER:100")
                throw new OfxParseException("Incorrect header format");

            if (header[1] != "DATA:OFXSGML")
                throw new OfxParseException("Data type unsupported: " + header[1] + ". OFXSGML required");

            if (header[2] != "VERSION:102")
                throw new OfxParseException("OFX version unsupported. " + header[2]);

            if (header[3] != "SECURITY:NONE")
                throw new OfxParseException("OFX security unsupported");

            if (header[4] != "ENCODING:USASCII")
                throw new OfxParseException("ASCII Format unsupported:" + header[4]);

            if (header[5] != "CHARSET:1252")
                throw new OfxParseException("Character set unsupported:" + header[5]);

            if (header[6] != "COMPRESSION:NONE")
                throw new OfxParseException("Compression unsupported");

            if (header[7] != "OLDFILEUID:NONE")
                throw new OfxParseException("OLDFILEUID incorrect");
        }

        #region Nested type: OFXSection

        /// <summary>
        /// Section of OFX Document
        /// </summary>
        private enum OfxSection
        {
            SIGNON,
            ACCOUNTINFO,
            TRANSACTIONS,
            BALANCE,
            CURRENCY
        }

        #endregion
    }
}