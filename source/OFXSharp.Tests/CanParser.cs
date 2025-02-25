namespace OFXSharp.Tests
{
    public class CanParser
    {
        [Fact]
        public void CanParserItau()
        {
            var parser = new OfxDocumentParser();
            var ofxDocument = parser.Import(new FileStream(@"itau.ofx", FileMode.Open));
        }

        [Fact]
        public void CanParserSantander()
        {
            var parser = new OfxDocumentParser();
            var ofxDocument = parser.Import(new FileStream(@"santander.ofx", FileMode.Open));
        }
    }
}