using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace sevenfloorsdown
{
    [TestClass]
    public class InFeedUnitTest
    {
        InFeed DUT;
        string _header = "\x02"; // STX
        string _footer = "\x0d" + "\x0a"; // CR + LF
        string plu = "061012";
        string ppk = "1700 ";
        string expectedCode;

        private void InitDut()
        {
            expectedCode = plu + "," + ppk;
            SerialSettings tmpSettings = new SerialSettings()
            {
                PortName = "COM5"
            };
            DUT = new InFeed(tmpSettings)
            {
                Header = _header,
                Footer = _footer,
                MessageFormat = "^[0-9a-zA-z]{6,6},[0-9]{4,4} $",
                CheckMessageFormat = false
            };
        }

        private void InitDutCheckFormat()
        {
            InitDut();
            DUT.CheckMessageFormat = true;
        }

        [TestMethod]
        public void RegexTest1()
        {
            InitDut();
            string testString = _header + expectedCode + _footer;
            Assert.IsTrue(DUT.BufferDataUpdated(testString));
            Assert.IsTrue(DUT.IsInCorrectFormat(expectedCode));
        }

        [TestMethod]
        public void RegexTest2()
        {
            InitDut();
            DUT.MessageFormat = "";
            Assert.IsTrue(DUT.IsInCorrectFormat(""));
        }

        [TestMethod]
        public void RegexTest3()
        {
            InitDut();
            string testString = _header + plu + "7," + ppk + _footer;
            Assert.IsTrue(DUT.BufferDataUpdated(testString));
            Assert.IsFalse(DUT.IsInCorrectFormat(testString));
        }

        [TestMethod]
        public void RegexTest4()
        {
            InitDut();
            plu = "06101";
            string testString = _header + plu + "," + ppk + _footer;
            Assert.IsTrue(DUT.BufferDataUpdated(testString));
            Assert.IsFalse(DUT.IsInCorrectFormat(testString));
        }

        [TestMethod]
        public void RegexTest5()
        {
            InitDut();
            string testString = _header + "arbitrary string" + _footer;
            Assert.IsTrue(DUT.BufferDataUpdated(testString));
            Assert.IsFalse(DUT.IsInCorrectFormat(testString));
        }
    

        [TestMethod]
        public void SimpleInputTest1()
        {
            InitDut();
            string testString = _header + expectedCode + _footer;
            Assert.IsTrue(DUT.BufferDataUpdated(testString));
            Assert.AreEqual(expectedCode, DUT.InFeedData);
        }

        [TestMethod]
        public void SimpleInputTest2()
        {
            InitDut();
            string testString = "randomish data" + _header + expectedCode + _footer;
            Assert.IsTrue(DUT.BufferDataUpdated(testString));
            Assert.AreEqual(expectedCode, DUT.InFeedData);
        }

        [TestMethod]
        public void SimpleInputTest3()
        {
            InitDut();
            string testString = _header + expectedCode + _footer + "randomish data";
            Assert.IsTrue(DUT.BufferDataUpdated(testString));
            Assert.AreEqual(expectedCode, DUT.InFeedData);
        }

        [TestMethod]
        public void MultipleInputTest1()
        {
            InitDut();
            string[] testData = new string[]
            {
                _header + plu,
                "," + ppk + _footer,
                "eenie",
                "meanine",
                "miney",
                "moe"
            };

            foreach (string fragment in testData)
            {
                if (DUT.BufferDataUpdated(fragment)) break;
            }

            Assert.AreEqual(expectedCode, DUT.InFeedData);
        }

        [TestMethod]
        public void MultipleInputTest2()
        {
            InitDut();
            string[] testData = new string[]
            {
                _header,
                plu,
                "," + ppk,
                _footer,
                "eenie",
                "meanine",
                "miney",
                "moe"
            };

            foreach (string fragment in testData)
            {
                if (DUT.BufferDataUpdated(fragment)) break;
            }

            Assert.AreEqual(expectedCode, DUT.InFeedData);
        }

        [TestMethod]
        public void MultipleInputTest3()
        {
            InitDut();
            string[] testData = new string[]
            {
                "randomish string" + _header,
                plu,
                "," + ppk,
                _footer + "another randomish string",
                "eenie",
                "meanine",
                "miney",
                "moe"
            };

            foreach (string fragment in testData)
            {
                if (DUT.BufferDataUpdated(fragment)) break;
            }

            Assert.AreEqual(expectedCode, DUT.InFeedData);
        }

        [TestMethod]
        public void MultipleInputTest4()
        {
            InitDut();
            string[] testData = new string[]
            {
                "peaches" + _header + plu,
                "," + ppk + _footer + "cream",
                "eenie",
                "meanine",
                "miney",
                "moe"
            };

            foreach (string fragment in testData)
            {
                if (DUT.BufferDataUpdated(fragment)) break;
            }

            Assert.AreEqual(expectedCode, DUT.InFeedData);
        }

        [TestMethod]
        public void MultipleInputTest5()
        {
            InitDut();
            string[] testData = new string[]
            {
                "eenie",
                "meanine",
                "miney",
                "moe",
                _header + plu,
                "," + ppk + _footer
            };

            foreach (string fragment in testData)
            {
                if (DUT.BufferDataUpdated(fragment)) break;
            }

            Assert.AreEqual(expectedCode, DUT.InFeedData);
        }

        [TestMethod]
        public void MultipleInputTest6()
        {
            InitDut();
            string[] testData = new string[]
            {
                "eenie",
                "meanine",
                "miney",
                "moe",
                "peaches" + _header + plu,
                "," + ppk + _footer + "cream"
            };

            foreach (string fragment in testData)
            {
                if (DUT.BufferDataUpdated(fragment)) break;
            }

            Assert.AreEqual(expectedCode, DUT.InFeedData);
        }

        [TestMethod]
        public void MultipleInputTest7()
        {
            InitDut();
            string[] testData = new string[]
            {
                "eenie",
                "meanine",
                "miney",
                "moe",
                _header,
                plu,
                "," + ppk,
                _footer
            };

            foreach (string fragment in testData)
            {
                if (DUT.BufferDataUpdated(fragment)) break;
            }

            Assert.AreEqual(expectedCode, DUT.InFeedData);
        }


        [TestMethod]
        public void SimpleInputFormatCheckTest1()
        {
            InitDutCheckFormat();
            string testString = _header + expectedCode + _footer;
            Assert.IsTrue(DUT.BufferDataUpdated(testString));
            Assert.AreEqual(expectedCode, DUT.InFeedData);
        }

        [TestMethod]
        public void SimpleInputFormatCheckTest2()
        {
            InitDutCheckFormat();
            string testString = "randomish data" + _header + expectedCode + _footer;
            Assert.IsTrue(DUT.BufferDataUpdated(testString));
            Assert.AreEqual(expectedCode, DUT.InFeedData);
        }

        [TestMethod]
        public void SimpleInputFormatCheckTest3()
        {
            InitDutCheckFormat();
            string testString = _header + expectedCode + _footer + "randomish data";
            Assert.IsTrue(DUT.BufferDataUpdated(testString));
            Assert.AreEqual(expectedCode, DUT.InFeedData);
        }

        [TestMethod]
        public void SimpleInputFormatCheckTest4()
        {
            InitDutCheckFormat();
            string testString = "arbitrary1" + _header + plu + "6," + ppk + _footer + "arbitrary2";
            Assert.IsFalse(DUT.BufferDataUpdated(testString));
            Assert.IsTrue(String.IsNullOrEmpty(DUT.InFeedData));
        }

        [TestMethod]
        public void MultipleInputFormatCheckTest1()
        {
            InitDutCheckFormat();
            string[] testData = new string[]
            {
                _header + plu,
                "," + ppk + _footer,
                "eenie",
                "meanine",
                "miney",
                "moe"
            };

            foreach (string fragment in testData)
            {
                if (DUT.BufferDataUpdated(fragment)) break;
            }

            Assert.AreEqual(expectedCode, DUT.InFeedData);
        }

        [TestMethod]
        public void MultipleInputFormatCheckTest2()
        {
            InitDutCheckFormat();
            string[] testData = new string[]
            {
                _header,
                plu,
                "," + ppk,
                _footer,
                "eenie",
                "meanine",
                "miney",
                "moe"
            };

            foreach (string fragment in testData)
            {
                if (DUT.BufferDataUpdated(fragment)) break;
            }

            Assert.AreEqual(expectedCode, DUT.InFeedData);
        }

        [TestMethod]
        public void MultipleInputFormatCheckTest3()
        {
            InitDutCheckFormat();
            string[] testData = new string[]
            {
                "randomish string" + _header,
                plu,
                "," + ppk,
                _footer + "another randomish string",
                "eenie",
                "meanine",
                "miney",
                "moe"
            };

            foreach (string fragment in testData)
            {
                if (DUT.BufferDataUpdated(fragment)) break;
            }

            Assert.AreEqual(expectedCode, DUT.InFeedData);
        }

        [TestMethod]
        public void MultipleInputFormatCheckTest4()
        {
            InitDutCheckFormat();
            string[] testData = new string[]
            {
                "peaches" + _header + plu,
                "," + ppk + _footer + "cream",
                "eenie",
                "meanine",
                "miney",
                "moe"
            };

            foreach (string fragment in testData)
            {
                if (DUT.BufferDataUpdated(fragment)) break;
            }

            Assert.AreEqual(expectedCode, DUT.InFeedData);
        }

        [TestMethod]
        public void MultipleInputFormatCheckTest5()
        {
            InitDutCheckFormat();
            string[] testData = new string[]
            {
                "eenie",
                "meanine",
                "miney",
                "moe",
                _header + plu,
                "," + ppk + _footer
            };

            foreach (string fragment in testData)
            {
                if (DUT.BufferDataUpdated(fragment)) break;
            }

            Assert.AreEqual(expectedCode, DUT.InFeedData);
        }

        [TestMethod]
        public void MultipleInputFormatCheckTest6()
        {
            InitDutCheckFormat();
            string[] testData = new string[]
            {
                "eenie",
                "meanine",
                "miney",
                "moe",
                "peaches" + _header + plu,
                "," + ppk + _footer + "cream"
            };

            foreach (string fragment in testData)
            {
                if (DUT.BufferDataUpdated(fragment)) break;
            }

            Assert.AreEqual(expectedCode, DUT.InFeedData);
        }

        [TestMethod]
        public void MultipleInputFormatCheckTest7()
        {
            InitDutCheckFormat();
            string[] testData = new string[]
            {
                "eenie",
                "meanine",
                "miney",
                "moe",
                _header,
                plu,
                "," + ppk,
                _footer
            };

            foreach (string fragment in testData)
            {
                if (DUT.BufferDataUpdated(fragment)) break;
            }

            Assert.AreEqual(expectedCode, DUT.InFeedData);
        }

        [TestMethod]
        public void MultipleInputFormatCheckTest8()
        {
            InitDutCheckFormat();
            string[] testData = new string[]
            {
                "eenie",
                "meanine",
                "miney",
                "moe",
                _header,
                plu,
                "78," + ppk + "arb",
                _footer
            };

            foreach (string fragment in testData)
            {
                Assert.IsFalse(DUT.BufferDataUpdated(fragment));
            }
            Assert.IsTrue(String.IsNullOrEmpty(DUT.InFeedData));
        }
    }
}
