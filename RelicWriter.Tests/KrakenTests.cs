using RelicWriter.GameAssets;
using Xunit;

namespace RelicWriter.Tests;

/// <summary>
/// The managed Kraken decoder, on streams made by Oodle's own compressor from synthetic data (a linear congruential generator, no game data):
/// a Huffman coded block, a tANS block, an LZ chunk with literals and matches, and run length coding. Far match distances (code 0xF0 and up) are only
/// covered by the real vehicle atlas, which has no synthetic stand-in.
/// </summary>
public class KrakenTests
{
    static IEnumerable<int> Random(int seed)
    {
        var s = seed;
        while (true) { s = (int)(((long)s * 1103515245 + 12345) & 0x7fffffff); yield return s >> 16; }
    }

    static byte[] Uniform16() => Random(1).Take(3000).Select(x => (byte)(x % 16)).ToArray();

    static byte[] Skewed() => Random(2).Take(4000).Select(x => (byte)(x % 100 < 95 ? 0 : 1)).ToArray();

    static byte[] Text()
    {
        string[] words = ["vehicle", "record", "engine", "night", "city", "cyber", "punk", "relic", "writer", "0123456789"];
        var r = Random(3).GetEnumerator();
        var text = new List<byte>();
        while (text.Count < 4000) { r.MoveNext(); text.AddRange(System.Text.Encoding.ASCII.GetBytes(words[r.Current % words.Length] + " ")); }
        return text.Take(4000).ToArray();
    }

    static byte[] Runs()
    {
        var r = Random(4).GetEnumerator();
        int Next() { r.MoveNext(); return r.Current; }
        var data = new List<byte>();
        while (data.Count < 3000)
        {
            for (var i = Next() % 6; i > 0; i--) data.Add((byte)(Next() & 255));
            data.AddRange(new byte[Next() % 40]);
        }
        return data.Take(3000).ToArray();
    }

    static readonly byte[] HuffmanStream = Convert.FromHexString(
        "8c060005ee402edc05ea80f0008222abfef00200fa00d664e3489e63147a15f56f51756b803095f41448a39fa5de97bd" +
        "bbfb512c8549e07a0d6736d54e610e6c7cdce7ac635a0fda6720ed6418a82f834e6c52c66fd350b54aafbc007f89bda7" +
        "8812b82435beb5753a8ce1cbb54e263f45171f887ee38ea7798a30af398686b9b50942452dcafb9fbeddacf0c62dd2f4" +
        "d96bc10a68e9fa22c11f9d91de2f51d2c72c2285bfb0163a120987dbd21d69567c04307887d44955cc5f2ab136569048" +
        "cbc822fc96d1666333055ee4aa81123d5e6dce3eb7c1d2983e9c3b84f9a9bf073efa44ba6f335b9c57b88fc085b4fe68" +
        "86e11b5ad87d56f97ec34aa2cfc9eae30d6bbf5fad8b25cd8da366fe7071b01ed8f2f8e61ed528dec997cf6b960fafb9" +
        "c1a64c8366663288800ccb67c6d1f0ae489f9f0daed342aad334597b83c7d8f284ae3b3db282f10b1ac64d60b4898521" +
        "6a81cadc6a869197fcba43ef773c9c355821a0eb10491b9c5f414492d5b4dabca76f3217df366be4ffde9e4ab7dff685" +
        "720e7da8e11a54d21b0984bdb0f8b001fd96a3632abb3c70d01e0dfb8508358af35ae021572e21e080d90d42446e6bb9" +
        "675d7be3092d99f54646e171145d4f5dd9880ba80f60afb6612e73583822e9c21a1f6e9f098f7097a052b77621ddf273" +
        "25eb9c1757b07631ead2b6c09bee0ec56824f93a26c5d1732f002b8e4508c55ee85e84bb19d677622600d47116a2118e" +
        "d0cc3e5fa97b43d56f71f4835961a021132b4e5da9c486cb0ddb6e3a6d72c47cb7d21e76d239b6b6795d19192a67d6e8" +
        "4bff20f5f4837a4b904dbf3437af2a61049109ab9394355de13cb8866faf6628ba86cd1185740bb6450381f75fd69492" +
        "c3f0abf204aa0e4e07a7037c8e43a0b9985ffecebf92604635a04cad4709c9e1e3d3e435f60a5725d9a3febfa3725209" +
        "a25be68a88c8438b6809f07e452e890c387d69d01198ef86653fdafd8f99c870c307364cb43d483b834a5f9cd2e8acce" +
        "8b1f44b69e815599805ac22ee0c9016f05b271a8c0c0e88277da8fefb311be8417965b4dca1d87327b7f9634af3ac956" +
        "f783bdd7fa008f2d58e3d6bdffb8d8956d105ea6e82db595ed1c655dc7a2c4939d56b58df047d2b45a23bf0e9f4d86d3" +
        "e0dc44d0b255c21d76af13e619777a1890af769d16267c47168205e09a0a869cec70df2697e521444f82b0ebcdf1c42d" +
        "1c008a21e2bf3b4f2e7aae86f4afd84f268ae0495f3d711bae78ced42c1c45d92259ae28d171c872be5f31f9d64df6e0" +
        "fec22c49869b445ba75e58ccd49f5b73ba23d36eace4286f2a64ad8130ee361aeea5c9ea7a7f0614cc6e27f0e1433532" +
        "a27eccb522eb7d97944bf862c4c2c820bae5240f055ce64df147fb7d9a4914e0541dbbe55bf9c0983b74d50b05e42e3f" +
        "4c94eb3a1a7e90e91d7f3f44015e7ab4d974960886bd0872e24730330836fccd52a5a93f088be3d099cf9872e19dbb4d" +
        "9ade7d749a06ae62f199e65a3525ad2af250d2bc29874d9109d2f6e1e863814269576b459abb683c6a4ee5b2026919c7" +
        "ab496baf0835906714ad11a4f3318e03265143398627c0f8b5b159e53f8cad0657d2c44025f0390cf28626527e30b3dc" +
        "833a4e378ea53c0ba193066efd6ef453f16ca229f2b8deee677ca8236ce82aa97d0aa045190083f9dfe4d0dddea62b48" +
        "2d425cb1cf2ba5b5b8b4992da0571c3b54ae3ab3e32889dc47ed966a7a70671a9e8e27d77562d80dcb7a87983b1aea00" +
        "e4b3e676704bbda31e9ed966140acc3eb38706fd060ab74394851da6925ba47063b347c2c33d1e12030d4038bbf54d16" +
        "479012a5653284a03efe2de9f696af6fd19dc5996ab53a4d02ba628a91fd6e049fccd569126c8ee76387080f455fb5b9" +
        "ab0863a4af0918dfa6a1dbb74a8d40e5a5eb7e736fc455eead737b09807174cfcb8f5c9f4deeb5c774e48309e263ee37" +
        "676f49bc83c0d56d92643f6eb120e110667a70c0d906d491b122e0595273a307d30968471d6fa923869fdec1a8b15dd7" +
        "79a4ccc289324e7f0e3b7d53969337c962a53f733ddb1a01f46a52e7d390abbe56e193d3c5d88548e0f76d8225e33f20" +
        "262c4c87b7c527d081745e8eb1119373332af591cb3f952f2c3ef0f7bfed2b2c187293d2");

    static readonly byte[] TansStream = Convert.FromHexString(
        "8c060000ac103e7c00a840c03d000063bb752a6915591e0fc8b4ecb9d9b281f97c699ffc7c0d474ee772369a41def988" +
        "78b7d6bc23819a944fc2cad6ee5da6b8b84e6256b753ba2c9ee24886fb055fc9765c6d7e5a1395ba980000041e0113a4" +
        "400afcf5449110618d2298759d9c12b7cc0cce44ad34a899044e5d6183ba9800053523389668ef514bbc0b1078edd92a" +
        "c785370d78cadeb7ab42c430255e3f33bab18c2ff93f6a8e81e5e71cbf9ee7359de2");

    static readonly byte[] LzStream = Convert.FromHexString(
        "8c060002af8802ad72656c696320726500005d6c696320637962657220656e67696e6520656e67696e652070756e6b20" +
        "6e69676874206e6967687476656869636c63697479203031323334353637383920777269746572636f72646369747976" +
        "6570756e6b20656e67696e65207665682004e000a391bed775be2a642411946db36c66c663186dd7bd736100e54f4000" +
        "000bca808b50002c009ff97bf2eb9e99c9e7e47ee6f23e9ac38fe3d23a256288d3e6dc1b97cb20d26d9f5f8a495f0bbc" +
        "51eb126401738ee79ff1be67263ab4b33de8e454a67a487e1f5dab1789dcd2a9d2fb8595d7d7d5e886cd0a7aad7e000f" +
        "ba589bd7e88bd8f1a2da3bf69a0f33f28bc842a6f95ff1c2d6dedb93619e64cc4e563eef0f9cff3380a0e4e383b0d419" +
        "7e94422b0ad967f669b309854d84c833904400d7902492c9f31565f02875242d6db2dcdcba3e239668a1ca4c1ba8ba44" +
        "f2e467a231797127e6677f9a0c71887ce085e47fa51d9bddbd901d7f65fa7fb469ec3367edc9cf018041c003aa90bc7e" +
        "47246280d2a1b5776a86cfa9cf0407e2c0d51463614aaee9da78556f291b3be459516167abb0edb7d98ad22f65dbcd16" +
        "791da4a5d9f764eddd0a0013c992f9a2eb6b7bd4bcbb75d2bdc2db86eabdc6c30fce1862d76f5b677d53304fe37872eb" +
        "68ce17151708878187f30f2d1f100538aa5cc8607a172fae22b21c0b00071000001b2503130102010101011301020c01" +
        "010204010106010903080b02012d3be17b1f90afc16ed88fb751074f46d157b0c486f97f96837c2d92cbf69a61b2c14c" +
        "3a608421e6f6dd7c2d0ac6f606433d8ab898d1ded7c7ab417311fd12f0d3afe302183a072843d769205a74f15c670807" +
        "d979548a1e2b7efdbbce7c56cc14916fdfeca62eaede595f0f99f5639d21add0e4c9e9f5abbea8cc5b064d36cd728f82" +
        "334df0cd00a7fbb4d8dd511791435800ad0b0817c1");

    static readonly byte[] RleStream = Convert.FromHexString(
        "8c06000218302edc02140066fb7f2f90424b45799de5808129caf3430fc7e8f0f3766456ca39130e610ec5fc940f2859" +
        "3ed71237c1cf85cd538df8f2f93d38b1327f5fbb926a0308dc92849ce700006c242aa2a14d89c4aa278d000a0b36623d" +
        "27c21583dcf12a740b71226002bfc561b1f50e651c3018f50e3e37ef24bf5dc26fd2d787d8ac5bd1d1b8589362cace42" +
        "b0f96bf43194bdc2b921a74a22002f38716aeb1c4a8f67309547002eb78078b3bcf198828c53ab0f310000ba93c5e664" +
        "02cfd1c247c996988da34f4157d219fbb214925414b9d67a9afaabf096c0ed576736caf8be859fef6e3d172f4d724c19" +
        "ecabad2792639ad0c01cfce7419025584a6941f615947326fb6c85f4441783e5147f8948635bb3dfd62ccff676f9e223" +
        "634fdb500bc2942bca466a4398b4d9173548f8a917f1a87afb6a57081ae4eaadd00bb7fef0aeb97936cd34cd457397a3" +
        "32fc0e69e8311f6a4457521b46722aac6d2f6339648b3a2ceaf90000dbaf998f8abfadbc7e851a812b8419dfcb4b4419" +
        "bebfaa6c8d6befeb9f8baa8319041ecfcd9f9eaf9baf9eddefecc4194cefda8318af9bc518051bfabfad451fc119fdab" +
        "4419041984190119ddfffd831cad8321fffddfccc319ebcfbac518dfdeefecdfca861c4519c3174719cfbddfdd051984" +
        "19afab3cc5188f8a821eaedfcdcfbdfffcefea0218ba6caf9a9ac118c2179f9e33dfcbcebfbdffebefea8319efec8419" +
        "bfabdfdb05187d3dffe8de7a9f9a");

    static void Check(byte[] stream, byte[] expected)
    {
        var actual = new byte[expected.Length];
        Assert.True(Kraken.TryDecompress(stream, actual));
        Assert.Equal(expected, actual);
    }

    [Fact] public void HuffmanBlock() => Check(HuffmanStream, Uniform16());

    [Fact] public void TansBlock() => Check(TansStream, Skewed());

    [Fact] public void LzChunk() => Check(LzStream, Text());

    [Fact] public void RunLengthBlock() => Check(RleStream, Runs());

    [Fact]
    public void OneByteRepeated()
    {
        // header, then the "same byte" marker, the byte
        var actual = new byte[3000];
        Assert.True(Kraken.TryDecompress([0x8C, 0x06, 0x07, 0xFF, 0xFF, 0x41], actual));
        Assert.All(actual, b => Assert.Equal(0x41, b));
    }

    // 3000 bytes of synthetic data each, compressed by Oodle: the first has an old-style Huffman header, the second a sparse tANS table.
    [Theory]
    [InlineData("8c06000080302edc007ca0981300c80406040400f6630f04dbb3e53f06c2fcf005109f8e8f8e8125bfbedfce81190009bfae8125011a7e00099e8122de042cae0009efde01288119011f7e411c0009011a8121012ebe811ddec11f41293e8f8e4123efde5e411b01224126411b4120dfcdde011a412c411b0009c11daedfde011b7e0009efdf",
        "ce6c23e351e444a8891f574edcf3a89fa19ac715c4e2cadb29e9d3086425c8d9")]
    [InlineData("8c060000d7102edc00d3320e0033c9dd873089239ce1544e5f7bcb2718391c1452c5217958e0b8f389b8dcac4839554e982f9497fd65ead87f47ae362acb03556636b630c3b01c29063c0694cd1012bd22a45f7746ad2da0b8ff35ac81b78abfa5016a8f2c769f7acb33e139a092204aa32754747c3800031b4549f0a40bfc53f78072ae8f4f40ed56864b32061bf475e660805367716da475520fdc061372411139010cb744ed05932dd722d64be19f3c9752e6e3fbfe7a183cb56ee30df4b1ae8039d17059efb2d09773dcbf8cf9c1b8927a64556962daaa241b301e",
        "04beff5c23b793823fa18e22a498ba123405f490fac84d01ebc2c3502e8f488d")]
    public void OldHuffmanHeaderAndSparseTansTable(string stream, string sha256)
    {
        var actual = new byte[3000];
        Assert.True(Kraken.TryDecompress(Convert.FromHexString(stream), actual));
        Assert.Equal(sha256, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(actual)));
    }

    [Fact]
    public void StoredBlockIsCopied()
    {
        byte[] data = [1, 2, 3, 4, 5];
        var actual = new byte[data.Length];
        Assert.True(Kraken.TryDecompress([0x4C, 0x06, .. data], actual));
        Assert.Equal(data, actual);
    }

    [Fact]
    public void DamagedStreamsFailInsteadOfThrowing()
    {
        var actual = new byte[3000];
        for (var cut = 0; cut < HuffmanStream.Length; cut += 97) Assert.False(Kraken.TryDecompress(HuffmanStream.AsSpan(0, cut), actual));
        Assert.False(Kraken.TryDecompress([], actual));
    }
}
