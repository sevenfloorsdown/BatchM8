using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace sevenfloorsdown
{
    [TestClass]
    public class OutFeedUnitTest
    {
        OutFeed DUT;
        int inputPLULen = 6;
        int inputPPKLen = 5;
        string inplu = "061012";
        string inppk = "1700 ";

        int outputPLULen = 5;
        int outputPPKLen = 4;
        string _outheader = "\x02"; // STX
        string _outfooter = "\x03"; // ETX
        string outplu = "61012";
        string outppk = "1700";
        string fixeddata = "abcdef";
        string inputMessage;
        string expectedData;

        private void InitDut()
        {
            inputMessage = inplu + "," + inppk;
            SerialSettings tmpSettings = new SerialSettings()
            {
                PortName = "COM5"
            };
            DUT = new OutFeed(tmpSettings)
            {
                Header = _outheader,
                Footer = _outfooter,
                InputPLULength = inputPLULen,
                InputPPKLength = inputPPKLen,
                OutputPLULength = outputPLULen,
                OutputPPKLength = outputPPKLen,
                FixedAsciiData = fixeddata
            };
            expectedData = outplu + outppk + fixeddata;
        }


        [TestMethod]
        public void SimpleOutputTest1()
        {
            InitDut();
            for (int i = 1; i < 10; i++)
            {
                DUT.InfeedNum = i.ToString();
                expectedData = DUT.InfeedNum + outplu + outppk + fixeddata;
                Assert.AreEqual(expectedData, DUT.CreateOutputMessage(inputMessage));
            }
        }

        [TestMethod]
        public void ExceptionOutputTest1()
        {
            InitDut();
            DUT.InputPLULength = DUT.OutputPLULength - 2;        
            DUT.InfeedNum = "1";
            try
            {
                string tmp = DUT.CreateOutputMessage(inputMessage);
                Assert.IsTrue(false);
            } 
            catch (Exception e)
            {
                Assert.IsTrue(true);
            }
        }

        [TestMethod]
        public void ExceptionOutputTest2()
        {
            InitDut();
            DUT.InputPPKLength = DUT.OutputPPKLength - 2;
            DUT.InfeedNum = "1";
            try
            {
                string tmp = DUT.CreateOutputMessage(inputMessage);
                Assert.IsTrue(false);
            }
            catch (Exception e)
            {
                Assert.IsTrue(true);
            }
        }
    }
}
