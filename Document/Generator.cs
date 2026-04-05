using Document.Layout;
using Document.Template;
using PdfSharp.Pdf;
using Shulkmaster.XDM;
using Shulkmaster.XDM.Lexer;
using Shulkmaster.XDM.Parser;

namespace Document;

public class Generator
{
    public async Task<MemoryStream> GeneratePdf(Stream input, CancellationToken cancellationToken)
    {
        var str = new MemoryStream();
        var reader = new TextStreamReader(input);
        var lexer = new XmlLexer(reader);
        var parser = new XmlParser(lexer);
        var doc = await parser.ParseAsync(cancellationToken);
        var media = new PdfMedia();
        
        var layout = new LayoutEngine(media);
        var pdf = new PdfDocument();
        pdf.Info.Title = "My PDF";
        
        var page = pdf.AddPage();

        var model = new TemplateBuilder();
        var template = model.FromXmlDoc(doc);
        
        layout.Layout(template, page.Width, page.Height);
        
        
        return str;
    }
}