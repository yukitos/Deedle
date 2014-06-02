(*** hide ***)
#load "../../../bin/Deedle.fsx"
#load "../../../packages/FSharp.Charting.0.90.6/FSharp.Charting.fsx"
#r "../../../packages/FSharp.Data.2.0.5/lib/net40/FSharp.Data.dll"
open System
open System.IO
open FSharp.Data
open Deedle
open FSharp.Charting

let root = __SOURCE_DIRECTORY__ + "/../data/"

(**
データフレームを F# で扱う
==========================

このセクションではF#データフレームライブラリの様々な機能を紹介します
(`Series` と `Frame` 型およびモジュールを使用します)。
任意のセクションに読み飛ばしてもらって構いませんが、
一部のセクションでは「作成および読み取り」で組み立てた値を参照している点に
注意してください。

また、このページをGitHubから
[F# スクリプトファイル](https://github.com/BlueMountainCapital/Deedle/blob/master/docs/content/frame.fsx)
としてダウンロードすれば、インタラクティブにサンプルを実行することもできます。

<a name="creating"></a>
フレームの作成とデータの読み取り
--------------------------------

<a name="creating-csv"></a>
### CSVファイルのロードとセーブ

データをデータフレームとして取得する一番簡単な方法は
CVSファイルを使用することです。
この機能は `Frame.ReadCsv` 関数として用意されています：
*)

// 'root' ディレクトリにファイルが置かれているものとします
let titanic = Frame.ReadCsv(root + "Titanic.csv")

// データを読み取ってインデックス列を設定し、行を並び替えます
let msft = 
  Frame.ReadCsv(root + "stocks/msft.csv") 
  |> Frame.indexRowsDate "Date"
  |> Frame.sortRowsByKey

// 列区切り文字を指定します
let air = Frame.ReadCsv(root + "AirQuality.csv", separators=";")
(**
`ReadCsv` メソッドにはロード時の動作を制御するためのオプションが多数あります。
このメソッドはCSVファイルやTSVファイル、その他の形式のファイルをサポートします。
ファイル名が`tsv`で終わる場合には自動的にタブ文字が処理されますが、
`separator` 引数を明示的に指定して区切り文字を設定することもできます。
以下の引数が有効です：

 * `path` - リソースのファイル名またはWeb上の位置を指定します。
 * `inferTypes` - メソッドが列の型を推測すべきかどうかを指定します。
   (スキーマを指定する場合には`false`を指定します)
 * `inferRows` - `interTypes=true` の場合、この引数には
   型の推論に使用する行数を指定します。
   デフォルトは0で、すべての行が使用されます。
 * `schema` - CSVスキーマを指定する文字列です。
   スキーマの形式については別途ドキュメントを参照してください。
 * `separators` - CSVファイルの行を区切る1つ以上の(単一)文字を文字列として
   指定します。
   たとえば `";"` とするとセミコロン区切りのファイルとしてパースされます。
 * `culture` - CVSファイルの値をパースする際に使用される
   カルチャ(たとえば `"en-US"` )を指定します。
   デフォルトはインバリアントカルチャです。

これらの引数は
[F# DataのCSV型プロバイダー](http://fsharp.github.io/FSharp.Data/library/CsvProvider.html)
([日本語](http://fsharp.github.io/FSharp.Data/ja/library/CsvProvider.html))
で使用されるものと同じものなので、そちらのドキュメントも参照してください。

データフレームを取得した後は、 `SaveCsv` メソッドを使用して
CSVファイルとして保存できます。
たとえば以下のようにします：
*)
// セミコロン区切りでCSVを保存します
air.SaveCsv(Path.GetTempFileName(), separator=';')
// 行キーを"Date"列として含むようなCSVファイルとして保存します
msft.SaveCsv(Path.GetTempFileName(), keyNames=["Date"], separator='\t')

(**
デフォルトでは `SaveCsv` メソッドはデータフレームのキーを含めません。
この動作は `SaveCsv` メソッドのオプション引数に `includeRowKeys=true` を指定するか、
(上の例のように) `keyNames` 引数に(複数の)キー列となるヘッダを設定します。
通常、行キーは1つだけですが、 [階層的インデックシング](#indexing)
が使用されている場合には複数になることもあります。

<a name="creating-recd"></a>
### F#レコードまたは.NETオブジェクトのロード

データをF#のレコード、C#の匿名型、あるいはその他の.NETオブジェクトとして返すような
.NETあるいはF#のコンポーネントを使用している場合には、
`Frame.ofRecords` を使用すればこれらをデータフレームに変換できます。
たとえば以下のデータがあるとします：
*)
type Person = 
  { Name:string; Age:int; Countries:string list; }

let peopleRecds = 
  [ { Name = "Joe"; Age = 51; Countries = [ "UK"; "US"; "UK"] }
    { Name = "Tomas"; Age = 28; Countries = [ "CZ"; "UK"; "US"; "CZ" ] }
    { Name = "Eve"; Age = 2; Countries = [ "FR" ] }
    { Name = "Suzanne"; Age = 15; Countries = [ "US" ] } ]
(**
そうすると `Person` のプロパティと同じ型のデータを持った
3つの列(`Name` `Age` `Countries`)を含んだデータフレームを
簡単に作成できます：
*)
// レコードのリストをデータフレームに変換します
let peopleList = Frame.ofRecords peopleRecds
// 'Name' 列を(文字列型の)キーとして使用します
let people = peopleList |> Frame.indexRowsString "Name"

(**
なおここでは列データに対する変換は何も行われないことに注意してください。
数値的なシリーズは `?` 演算子を使用してアクセスできます。
その他の型の場合には適切な型引数を指定して
`GetColumn` を明示的に呼び出す必要があります：
*)
people?Age
people.GetColumn<string list>("Countries")

(**
<a name="creating-wb"></a>
### F#データプロバイダ

一般的にはデータをタプルのシリーズとして公開するような
任意のデータソースを使用できます。
つまり、[F# Dataライブラリ](https://github.com/fsharp/FSharp.Data)
のWorld Bank型プロバイダーのような機能を使用すれば、
簡単にデータをロードできるというわけです。
*)
// World Bankに接続します
let wb = WorldBankData.GetDataContext()

/// 特定の地域に対して、GDPを現在のUSドルでロードし、
/// 2レベルの列キー(地域と国名)を持ったフレームとしてデータを返します
let loadRegion (region:WorldBankData.ServiceTypes.Region) =
  [ for country in region.Countries -> 
      // タプルを使用して、2レベルの列キーを作成します
      (region.Name, country.Name) => 
        // WorldBankから返された複数のタプルからシリーズを作成します
        Series.ofObservations country.Indicators.``GDP (current US$)`` ]
  |> frame

(**
データ処理をもっと簡単にするために、
地域毎の国情報を読み取って階層的インデックスを持ったデータフレームを作成しています
(詳細については[高度なインデックシングのセクション](#indexing)を参照)。
これでOECDとユーロエリアのデータを簡単に読み取ることができるようになりました：
*)
// ユーロとOECD地域をロード
let eu = loadRegion wb.Regions.``Euro area``
let oecd = loadRegion wb.Regions.``OECD members``

// これらをジョインして、10億USドル単位に変換します
let world = eu.Join(oecd) / 1e9

(*** include-value:(round (world*100.0))/100.0 ***)

(**
読み取ったデータはおよそ上のようになります。
見ての通り、列は地域によってグループ化され、一部のデータが値無しになっています。

### オブジェクトを列に展開する

シリーズ内のメンバーとして他の.NETオブジェクトを含むようなデータフレームを
作成することもできます。
これはたとえば複数のデータソースから生成されたオブジェクトを取得して、
これらを処理する前にアラインまたは連結したいような場合に便利なことがあります。
しかし複雑な.NETオブジェクトを含むフレームの場合には、
逆に利便性が損なわれる場合もあります。

そのため、データフレームは**展開(expansion)**機能をサポートしています。
列に何かしらのオブジェクトを含んだデータフレームに対して
`Frame.expandCols`を使用すると、オブジェクトのプロパティそれぞれを新しい列として
含むような新しいフレームを作成できます。
たとえば以下のようにします：
*)

(*** define-output:ppl ***)
// 'People'という1列だけを持ったフレームを作成します
let peopleNested = 
  [ "People" => Series.ofValues peopleRecds ] |> frame

// 'People'列を展開します
peopleNested |> Frame.expandCols ["People"]

(*** include-it:ppl ***)

(**
見ての通り、この操作では元の列の型に含まれるプロパティを元にして
複数の列が生成されて、それぞれの列の名前は
元の列の名前に各プロパティの名前を続けたものになります。

.NETオブジェクトだけでなく、`IDictionary<K, V>`型の値や
`string`をキーとするネストされたシリーズ(つまり`Series<string, T>`)
について展開することができます。
さらに複雑な構造をしている場合には、
`Frame.expandAllCols` を使用して特定のレベルまで再帰的に列を展開することもできます：

*)
// タプルを含むディクショナリを含んだシリーズ
let tuples = 
  [ dict ["A", box 1; "C", box (2, 3)]
    dict ["B", box 1; "C", box (3, 4)] ] 
  |> Series.ofValues

// ディクショナリのキー(レベル1)とタプルのアイテム(レベル2)を展開
frame ["Tuples" => tuples]
|> Frame.expandAllCols 2

(**
この場合、結果のデータフレームには `Tuples.A` と `Tuples.B`、
さらにディレクトリ内にネストされたタプルの2つの項目を表す
`Tuples.C.Item1` と `Tuples.C.Item2` の列が含まれることになります。

<a name="dataframe"></a>
データフレームの操作
--------------------

シリーズ型 `Series<K, V>` はシリーズのキーの型が `K` で
値の型が `V` であることを表しています。
つまりシリーズを処理する場合には、値の型があらかじめわかっているということです。
一方、データフレームの場合にはそうはいきません。
`Frame<R, C>` の `R` と `C` はそれぞれ行と列のインデックスの型を表します
(一般的に `R` は `int` または `DateTime` で、`C` は異なる行または列名を表す
`string` になります)。

フレームには多様なデータが含まれます。
ある列には整数、別の列には浮動小数点数、さらに別の列には文字列や日付、
あるいは文字列のリストのような別のオブジェクトが含まれることがあります。
これらの情報は静的にキャプチャされません。
したがってシリーズの読み取りなど、フレームを操作する場合には、
明示的に型を指定しなければいけないことがあります。

### フレームからデータを取得する

ここでは `string` 型の `Name` と、`int` 型の `Age`、`string list` 型の `Countries`
という3つの列を持った `people` データフレームを使用します
(これは [先のセクションで](#creating-recd) F#のレコードから作成したものです)：

               Name    Age Countries
    Joe     -> Joe     51  [UK; US; UK]
    Tomas   -> Tomas   28  [CZ; UK; US; ... ]
    Eve     -> Eve     2   [FR]
    Suzanne -> Suzanne 15  [US]

フレーム `df` から列(シリーズ)を取得するには
データフレームで直接公開されている操作を使用するか、
`df.Columns` を使用してフレームのすべての列を
シリーズのシリーズとして取得することもできます。
*)

// 'Age' 列を 'float' 値のシリーズとして取得します
// ('?' 演算子が値を自動的に変換します)
people?Age
// 'Name' 列を 'string' 値のシリーズとして取得します
people.GetColumn<string>("Name")
// フレームのすべての列をシリーズのシリーズとして取得します
people.Columns

(**
`Series<string, V>` 型 は疑問符演算子をサポートしているため、
この型の `s` に対して `s?Foo` とすると、
キー `Foo` に関連づけられた型 `V` の値を取得できます。
それ以外の型を取得する場合には `Get` メソッドを使用します。
なおフレームの場合と異なり、明示的な変換は行われないことに注意してください：
*)
// Series<string, float> を取得します
let numAges = people?Age

// 疑問符演算子を使用して値を取得します
numAges?Tomas
// 'Get' メソッドを使用して値を取得します
numAges.Get("Tomas")
// キーが見つからない場合には値無しを返します
numAges.TryGet("Fridrich")

(**
疑問符演算子と `Get` メソッドはデータフレームの
`Columns` プロパティに対しても使用できます。
`df?Columns` の返り値の型は ｀ColumnSeries<string, string>` で、
これは単に `Series<C, ObjectSeries<R>>` の薄いラッパーです。
つまり各列を表す `ObjectSeries<R>` を値に持つような列の名前で
インデックスされたシリーズを取得しなおすことができるというわけです。
`ObjectSeries<R>` 型は `Series<R, obj>` の薄いラッパーで、
これは値を特定の型として取得できるような機能がいくつか追加されたものです。

今回の場合、返される値は `ObjectSeries<string>`
として表されるそれぞれの列になります：
*)
// ObjectSeriesとして列を取得します
people.Columns?Age
people.Columns?Countries
// [fsi:val it : ObjectSeries<string> =]
// [fsi:  Joe     -> [UK; US; UK]       ]
// [fsi:  Tomas   -> [CZ; UK; US; ... ] ]
// [fsi:  Eve     -> [FR]               ]
// [fsi:  Suzanne -> [US]               ]

// メンバーを使用して列の取得を試みます
people.Columns.Get("Name")
people.Columns.TryGet("CreditCard")
// 特定のオフセットにある列を取得します
people.Columns.GetAt(0)

// 列をObjectSeriesとして取得し、
// それを型付きの Series<string, string> に変換します
people.Columns?Name.As<string>()
// 列をSeries<string, int>として変換するよう試みます
people.Columns?Name.TryAs<int>()

(**
`ObjectSeries<string>` 型には元の `Series<K, V>` 型に
若干のメソッドが追加されています。
18行目と20行目では `As<T>` と `TryAs<T>` を使用して、ObjectSeriesを
静的に既知の型を持った値のシリーズに変換しています。
18行目の式は `people.GetColumn<string>("Name")` という式と同じものですが、
`As<T>` の対象はフレームの行だけではありません。
データセットの行がいずれも同じ型の場合には、フレームの行に対して
(`people.Rows`を使用して)同じ操作ができます。

`ObjectSeries<T>` を扱わなければならないケースはもう1つ、
行をマッピングする場合です：
*)
// 行を走査して、国リストの長さを取得します
people.Rows |> Series.mapValues (fun row ->
  row.GetAs<string list>("Countries").Length)

(**
`people.Rows` から返された行は混種である(つまり異なる型の値が含まれている)ため、
シリーズのすべての値を `row.As<T>()` で何かしらの型に変換することはできません。
その代わり、 `Get(...)` と同じような `GetAs<T>(...)` を使用すると、
値を特定の型に変換して取得することができます。
`row?Countries` とすれば結果が `string list` にキャストされた状態で取得できますが、
`GetAs` メソッドにはもう1つ便利な文法があります。

### 行および列の追加

シリーズ型は **不変** なので、シリーズに新しい値を追加したり、
既存のシリーズに含まれる値を変更したりすることはできません。
しかし `Merge` のような機能を使用して、新しいシリーズを含むような結果を
返す操作を行うことはできます。
*)

// さらに値を含んだシリーズを作成します
let more = series [ "John" => 48.0 ]
// シリーズが連結された新しいシリーズを作成します
people?Age.Merge(more)

(**
データフレームは非常に限定的に変更をサポートしています。
既存のデータフレームには(列として)新しいシリーズを追加したり、
削除あるいは置き換えたりすることもできます。
しかしシリーズそれぞれはやはり不変です。
*)
// すべての人物の年齢を1つ増やします
let add1 = people?Age |> Series.mapValues ((+) 1.0)

// 新しいシリーズとしてフレームに追加します
people?AgePlusOne <- add1

// 値のリストを使用して新しいシリーズを追加します
people?Siblings <- [0; 2; 1; 3]

// 既存のシリーズを新しい値に置き換えます
// (people?Siblings <- ... と同じです)
people.ReplaceColumn("Siblings", [3; 2; 1; 0])

(**
最後に、既存のデータフレームに1つのデータフレーム、
あるいは1つの行を追加することも可能です。
この操作も不変なので、行が追加された新しいデータフレームが返されることになります。
データフレーム用の新しい行を作成するには、
キー値ペアからシリーズを作成する標準的な方法か、
あるいは `SeriesBuilder` 型を使用することもできます：
*)

// 必須の列に対応する値を持った新しいシリーズオブジェクトを作成します
let newRow = 
  [ "Name" => box "Jim"; "Age" => box 51;
    "Countries" => box ["US"]; "Siblings" => box 5 ]
  |> series
// 新しいシリーズを含んだ新しいデータフレームを作成します
people.Merge("Jim", newRow)

// 可変なSeriesBuilderオブジェクトを使用することもできます
let otherRow = SeriesBuilder<string>()
otherRow?Name <- "Jim"
otherRow?Age <- 51
otherRow?Countries <- ["US"]
otherRow?Siblings <- 5
// 組み立てたシリーズはSeriesプロパティで取得できます
people.Merge("Jim", otherRow.Series)

(**

<a name="slicing"></a>
高度なスライシングおよびルックアップ
------------------------------------

特定のシリーズからは多数の方法で1つ以上の値を取得したり、
(複数キーや関連する複数の値などの)観測データを取得したりできます。
まず(順序づけられていないシリーズも含む)任意のデータを
対象にできる差分ルックアップ操作について説明します。
*)

// 異なるキーと値を持ったサンプル用のシリーズ
let nums = series [ 1 => 10.0; 2 => 20.0 ]
let strs = series [ "en" => "Hi"; "cz" => "Ahoj" ]

// キーを使用して値を検索します
nums.[1]
strs.["en"]
// キーが文字列の場合には以下のようにアクセスできます
strs?en

(**
さらなる例として、[前の例で作成したデータセット](#creating-recd) 
の `Age` 列を使用します：
*)

// 順序づけられていないサンプルのシリーズを取得します
let ages = people?Age

// 特定のキーに対応する値を取得します
ages.["Tomas"]
// データソースから2つのキーを指定してシリーズを取得します
ages.[ ["Tomas"; "Joe"] ]

(**
`Series` モジュールには便利な関数が他にも用意されています
(たとえば `ages.TryGet` のように、多くはメンバーメソッドとしても
呼び出すことが出来ます)：
*)

// キーが存在しない場合には失敗します
try ages |> Series.get "John" with _ -> nan
// キーが存在志ない場合には'None'が返ります
ages |> Series.tryGet "John"
// 'John' の値を除いたシリーズが返ります
// ('ages.[ ["Tomas"; "John"] ]' と同じです)
ages |> Series.getAll [ "Tomas"; "John" ]

(**
シリーズからすべてのデータを取得することもできます。
データフレームライブラリではすべてのキー値ペアのことを
**observations(観測値)** と呼んでいます。
*)

// すべてのobservationsを'KeyValuePair'のシーケンスとして取得します
ages.Observations
// すべてのobservationsをタプルのシーケンスとして取得します
ages |> Series.observations
// 値無しに対しては'None'となるようにしてすべてのobservationsを取得します
ages |> Series.observationsAll

(**
これまでの例ではいずれもキーを厳密に指定して検索を行っていました。
順序付きシリーズの場合には、最も近くで利用可能なキーを使用して検索をしたり、
スライシングしたりすることができます。
[先の例で作成した](#creating-csv) MSFTの株価データで試してみましょう：
*)

// 始値をシリーズとして取得します
let opens = msft?Open

// キーがシリーズ内に無いため失敗します
try opens.[DateTime(2013, 1, 1)] with e -> nan
// 最も近い大きな値をキーにして値を探すため正しく動作します
opens.Get(DateTime(2013, 1, 1), Lookup.ExactOrSmaller)
// 最も近い小さな値をキーにして値を探すため正しく動作します
opens.Get(DateTime(2013, 1, 1), Lookup.ExactOrSmaller)

(**
インスタンスのメンバーを使用する場合、
`Lookup` を引数にとる `Get` メソッドのオーバーロードが使用できます。
同じ機能が `Series.lookup` として定義されています。
キーのシーケンスに対応する値を取得することもできます：
*)
// 最も近い大きなキーに対する値を取得します
opens |> Series.lookup (DateTime(2013, 1, 1)) Lookup.ExactOrGreater

// 2012年の各月における1つめの価格を取得します
let dates = [ for m in 1 .. 12 -> DateTime(2012, m, 1) ]
opens |> Series.lookupAll dates Lookup.ExactOrGreater

(**
順序付きシリーズに対しては、スライシングによって
シリーズの部分区間を取得することもできます：
*)

(*** define-output:opens ***)
opens.[DateTime(2013, 1, 1) .. DateTime(2013, 1, 31)]
|> Series.mapKeys (fun k -> k.ToShortDateString())

(*** include-it:opens ***)

(** 
スライシングはシリーズのキーが利用できない場合であっても機能します。
ルックアップ時には上限(最も近い上方の値のうち、最も小さい値)
および下限(最も近い下方の値のうち、最も大きい値)が自動的に使用されます
(今回の場合、1月1日に対応する値はありません)。

[後のセクション](#indexing) で説明しますが、
階層的(あるいはマルチレベル)インデックスを使用している場合には
まだいくつかの方法があります。
しかしまずはグループ化について説明する必要があるでしょう。

<a name="grouping"></a>
データのグループ化
------------------

データのグループ化は順序付きのシリーズやフレーム、順序無しのシリーズやフレームの
いずれに対しても行うことができます。
順序付きシリーズを対象とする場合には
(フローティングウィンドウや連続要素のグループ化など)
固有の機能を利用できます。
詳細については [時系列データのチュートリアル](series.html) を参照してください。
基本的には2つの機能があります：

 - 任意の値のシリーズをグループ化して、(それぞれのグループを)
   シリーズのシリーズとして取得できます
   `Frame.ofColumns` あるいは `Frame.ofRows` を使用することにより、
   結果を簡単にデータフレームへと変換できますが、この処理は自動的には行われません。

 - 特定の列に含まれる値、あるいは関数を使用してフレーム行をグループ化できます。
   この場合にはマルチレベル(階層的)インデックスを持ったフレームが返されます。
   階層的インデックスについては [後ほど説明します](#indexing)。

データフレームに対して `df.Rows` あるいは `df.Columns` とすれば
簡単に行シリーズ、あるいは列シリーズが取得できるわけなので、
データフレームに対する1番目の機能は特に有用だということを覚えておいてください。

### シリーズのグループ化

以下のサンプルでは、 [先のセクション](#creating-recd) でF#のレコードから
ロードしたデータフレーム `people` を使用しています。
まずデータを取得します：
*)
let travels = people.GetColumn<string list>("Countries")
// [fsi:val travels : Series<string,string list> =]
// [fsi:  Joe     -> [UK; US; UK]       ]
// [fsi:  Tomas   -> [CZ; UK; US; ... ] ]
// [fsi:  Eve     -> [FR]               ]
// [fsi:  Suzanne -> [US]               ]
(**
そうすると、キー(例：名前の長さ) と値(行ったことがある国の数)の
両方を使用して要素を取得することができます：
*)
// 名前の長さでグループ化 (行ったことがある国は無視)
travels |> Series.groupBy (fun k v -> k.Length)
// 行ったことがある国の数 (USに行ったことがある/無い人)
travels |> Series.groupBy (fun k v -> List.exists ((=) "US") v)

// 名前の長さでグループ化して各グループの値の個数を取得します
travels |> Series.groupInto 
  (fun k v -> k.Length) 
  (fun len people -> Series.countKeys people)
(**
`groupBy` 関数はシリーズのシリーズを返します(新しいキーを持ち、
特定の新しいキーに対するすべての値を含んだシリーズを値とします)。
そして `Series.mapValues` を使用すると値を変形させることができます。
しかし間でシリーズを全く確保したくないという場合には、
`Series.groupInto` を使用できます。
この関数の2番目の引数には射影関数を指定します。
上の例の場合には各グループのキーの数をカウントしています。

最後の例として、(行として)それぞれの人物と、
(列として)誰かしらが行ったことのある国を含むデータフレームを組み立ててみましょう。
フレームにはそれぞれの人物が行ったことのある国の数が含まれるようにします：
*)
(*** define-output: trav ***)
travels
|> Series.mapValues (Seq.countBy id >> series)
|> Frame.ofRows
|> Frame.fillMissingWith 0

(*** include-it: trav ***)

(**
この問題は `Series.mapValues` とF#の標準的な `Seq` 関数を
組み合わせるだけで解決できます。
まずすべての行(人物および行ったことのある国リスト)を走査します。
そしてそれぞれの国リストに対して、各国と訪問数を含むようなシリーズを生成します
(`Seq.countBy` と `series` を組み合わせることで観測データのシリーズを組み立てます)。
それからこの結果をデータフレームへと変換して、
値無しのデータを定数値0で置き換えています
([値無しへの対処](#missing)を参照してください)。

### データフレームのグループ化

これまではシリーズやシリーズのシリーズを対象にしてきました
(シリーズのシリーズは `Frame.ofRows` や `Frame.ofColumns` で
データフレームに変換できます)。
次に、データフレームを対象とする方法を説明しましょう。

ここでは [プロジェクトのホームページ](index.html) でも使用している、
[タイタニック号のデータセット](http://www.kaggle.com/c/titanic-gettingStarted)
が読み取り済みであるとします。
まず基本的なグループ化を紹介します(ホームページのデモでも使用しているものです)：
*)

// 'string'型の列'Sex'でグループ化
titanic |> Frame.groupRowsByString "Sex"

// 'decimal'に変換された列でグループ化
let byDecimal : Frame<decimal * _, _> = 
  titanic |> Frame.groupRowsBy "Fare"

// これはメンバーメソッドを使用すれば簡単に記述できます
titanic.GroupRowsBy<decimal>("Fare")

// 計算値(名前の長さ)でグループ化
titanic |> Frame.groupRowsUsing (fun k row -> 
  row.GetAs<string>("Name").Length)

(**
フレームを対象にする場合、行と列の両方のデータを使用してグループ化できます。
多くの関数にとって `groupRows` と `groupCols` は同じものです。
一番簡単に使用できる関数は `Frame.groupRowsByXyz` で、
`Xyz` にはグループ化に使用する列の型を指定します。
たとえば `Frame.groupRowsByString("Sex")` とすれば、
文字列型の列 "Sex" を使用して行を簡単にグループ化できます。

あまり一般的では無い型を使用する場合、列の型を指定する必要があります。
これは5行目と9行目で`decimal`をキーとして使用しているコードを参照してください。
最後に、キーセレクタを関数で指定することもできます。
この関数は元のキーと、`ObjectSeries<K>` 型の値である行を引数にとります。
`ObjectSeries<K>` には特定の型の列を取得する `GetAs` など、
それぞれの値(列)を取得するための様々なメンバーが定義されています。

### 単一キーでのグループ化

グループ化されたデータフレームではマルチレベルインデックスが使用されています。
これはつまり、インデックスが複数レベルを表すキーのタプルになっているということです。
たとえば以下の通りです：
*)
titanic |> Frame.groupRowsByString "Sex"
// [fsi:val it : Frame<(string * int),string> =]
// [fsi:                Survive   Name                    ]
// [fsi:  female 2   -> True      Heikkinen, Miss. Laina  ]
// [fsi:         11  -> True      Bonnell, Miss. Elizabeth]
// [fsi:         19  -> True      Masselmani, Mrs. Fatima ]
// [fsi:                ...       ...                     ]
// [fsi:  male   870 -> False     Balkic, Mr. Cerin       ]
// [fsi:         878 -> False     Laleff, Mr. Kristo      ]

(**
見ての通り、簡易プリンターはマルチレベルインデックスを理解して、
第1レベル(性別)に続けて第2レベル(乗船者ID)を出力します。
`Frame.unnest` や `Frame.nest` を使用すると、
2レベルインデックスをデータフレームのシリーズ1つに変換する(あるいは逆の変換をする)
ことができます：
*)
let bySex = titanic |> Frame.groupRowsByString "Sex" 
// 値として2つのフレームを持つようなシリーズを返します
let bySex1 = bySex |> Frame.nest
// スタックされていないデータを単一のフレームに戻します
let bySex2 = bySex |> Frame.nest |> Frame.unnest
(**

### 複数キーでのグループ化

最後に、複数のキーを使用してデータを繰り返しグループ化できるということを説明します。
たとえば乗船者を階級(Pclass)および乗船した場所(Embarked)でグループ化できます：
*)
// 階級および乗船場所で乗船者をグループ化します
let byClassAndPort = 
  titanic
  |> Frame.groupRowsByInt "Pclass"
  |> Frame.groupRowsByString "Embarked"
  |> Frame.mapRowKeys Pair.flatten3

// 同じ行インデックスを持ったAgeシリーズだけを取得します
let ageByClassAndPort = byClassAndPort?Age
(**
`byClassAndPort` の型を確認してみると、 `Frame<(string * int * int),string>`
になっていることがわかります。
行キーは乗船場の識別子(string)、乗船者の階級(1から3までのint)、
乗船者IDの3つ組です。
フレームから単一のフレームを取得すると、
マルチレベルインデックスは保持されます。

最後の例として、グループに対する様々な集計方法を紹介します：
*)
// 各グループの平均年齢を求めます
byClassAndPort?Age
|> Stats.levelMean Pair.get1And2Of3

// 数字的なすべての行に対して平均を求めます
byClassAndPort
|> Frame.getNumericCols
|> Series.dropMissing
|> Series.mapValues (Stats.levelMean Pair.get1And2Of3)
|> Frame.ofColumns

// 各グループの生存者数をカウントします
byClassAndPort.GetColumn<bool>("Survived")
|> Series.applyLevel Pair.get1And2Of3 (Series.values >> Seq.countBy id >> series)
|> Frame.ofRows

(**
2番目のスニペットでは便利な関数を複数組み合わせています。
まず、 `Frame.getNumericCols` を使用して、
データフレームから数字的な列だけを取得します。
そして `Series.dropMissing` を使用して、値無しの列を排除します。
それから `Series.mapValues` を使用して、すべての列に対して平均を計算しています。

最後のスニペットにも注目してください。
(ブール値が含まれた)"Survived"列を取得した後、
特定の関数を使用して各グループを集計しています。
この関数は3つのコンポーネントで構成されています。
まずグループ内の値を取得し、その値(つまり `true` と `false` の数)をカウントし、
この結果を使用してシリーズを作成しています。
実行結果は以下のようなテーブルになります(一部の値を省略してあります)：

             True  False     
    C 1  ->  59    26        
      2  ->  9     8         
      3  ->  25    41        
    S 1  ->  74    53        
      2  ->  76    88        
      3  ->  67    286       

<a name="pivot"></a>
ピボットテーブルを使用してデータを集計する
------------------------------------------

先のセクションでは非常に一般的なデータ処理操作であるグループ化を紹介しました。
しかしたとえば特定のキーでデータをグループ化しつつ、
集計結果を出力するというように、2つの操作を同時に行いたいこともよくあります。
この組み合わせは **ピボットテーブル** という概念としてまとめられています。

ピボットテーブルはデータフレームの行で利用可能な2つのキーを
元にしたフレーム内のデータを集計したい場合に便利なツールです。

たとえば [以前に読み込み](#creating-csv)、先のセクションでも扱った
タイタニック号のデータセットに対して、男性と女性の生存比率を比較したいとしましょう。
これはピボットテーブルを作成する呼び出しを1回行うだけで実現できます：
*)

(*** define-output:pivot1 ***)
titanic
|> Frame.pivotTable
    // 新しい行キーを返します
    (fun k r -> r.GetAs<string>("Sex"))
    // 新しい列キーを返します
    (fun k r -> r.GetAs<bool>("Survived"))
    // サブフレームに対する集計処理を指定します
    Frame.countRows

(**
`pivotTable` 関数(および対応する `PivotTable` メソッド)は
3つの引数をとります。
最初の2つには元のフレームの行を受け取って新しい行キーを返す関数、
および列を受け取って新しい列キーを返す関数をそれぞれ指定します。
上の例では `Sex` の値が新しい行キーで、
乗船者が生存したかどうかが新しい列キーになります。
その結果、以下のような2x2のテーブルになります：
*)

(*** include-it:pivot1 ***)

(**
ピボットテーブル操作では元となるフレームを受け取り、
データ(行)を新しい行キーと列キーで分割し、
そして指定された集計処理により各フレームを集計します。
上の例では単に各サブグループの合計人数を返せばよいため、
`Frame.countRows` を指定しています。
ですが、たとえば平均年齢のように別の統計情報を計算することも簡単にできます：
*) 

(*** define-output:pivot2 ***)
titanic 
|> Frame.pivotTable 
    (fun k r -> r.GetAs<string>("Sex")) 
    (fun k r -> r.GetAs<bool>("Survived")) 
    (fun frame -> frame?Age |> Stats.mean)
|> round
(**
この結果から、年配の男性は若者に比べて生存率が低い一方、
年配の女性は若者に比べて生存率が高いことがわかります：
*)

(*** include-it:pivot2 ***)

(**
<a name="indexing"></a>
階層的インデックシング
----------------------

一部のデータセットではインデックスが単純なキーのシーケンスではなく、
さらに複雑な階層構造をなしている場合があります。
これは階層的インデックスを使用することで処理することができます。
また多次元データを簡単に処理する方法もあります。
マルチレベルインデックスは最も一般的にはグループ化によって生成されます
(先のセクションにいくつか例があります)。

### 世界銀行データセットのルックアップ

このセクションでは [先ほど使用したものと同じ、世界銀行のデータセット](#creating-wb)
を見ていくことにします。
これは2レベルの列階層を持ったデータフレームで、
1レベル目が地域名、2レベル名が国名になっています。

基礎的なルックアップ操作としてはスライシング処理があります。
以下のコードはF# 3.1においてのみ有効です：
*)

// ユーロエリアの全国を取得します
world.Columns.["Euro area", *]
// ユーロエリアグループからベルギー(Belgium)のデータを取得します
world.Columns.[("Euro area", "Belgium")]
// ベルギーはユーロとOECD両方に属するため2つデータが返されます
world.Columns.[*, "Belgium"]

(**
F# 3.0の場合には以下のように `LookupXOfY` 系のヘルパ関数を使用します：
*)

// ユーロエリアの全国を取得します
world.Columns.[Lookup1Of2 "Euro area"]
// ベルギーはユーロとOECD両方に属するため2つデータが返されます
world.Columns.[Lookup2Of2 "Belgium"]

(**
ルックアップ操作では常に元のフレームと同じ型のデータフレームが返されます。
これはつまり1つのサブグループを選択したとしても、
マルチレベル階層キーを持った同じフレームを取得し直すことができるということです。
この動作はキーに対する射影を行うことで簡単に変更できます：
*)
// 1レベル目のキーを除去します(そして国名だけを取得します)
let euro = 
  world.Columns.["Euro area", *]
  |> Frame.mapColKeys snd

(**
### 世界銀行のデータに対するグループ化および集計

階層的キーはグループ化の結果として生成されることがよくあります。
たとえばユーロ地区のデータセットに対して10年単位で行をグループ化することができます
(グループ化の詳細についてはこのドキュメント内にある
 [グループ化のセクション](#grouping) を参照してください)。

*)
let decades = euro |> Frame.groupRowsUsing (fun k _ -> 
  sprintf "%d0s" (k / 10))
// [fsi: ]
// [fsi:val decades : Frame<(string * int),string> =]
// [fsi:                Austria  Estonia   ...      ]
// [fsi:  1960s 1960 -> 6.592    <missing> ]     
// [fsi:        1961 -> 7.311    <missing> ]
// [fsi:        ...  ]
// [fsi:  2010s 2010 -> 376.8    18.84 ]
// [fsi:        2011 -> 417.6    22.15 ]
// [fsi:        2012 -> 399.6    21.85 ]
(**
これで階層的インデックスを持ったデータフレームが用意出来たので、
たとえば1990年代のような1つのグループから
データを選択することが出来るようになりました。
その結果は同じ型を持ったデータフレームです。
値それぞれを積算すれば、USドル基準の(10億単位ではない)元々のGDPを計算できます：
*)
decades.Rows.["1990s", *] * 1e9

(**
`Frame` および `Series` モジュールには集計やグループ化を行うための関数が
多数定義されています。
たとえば特定の国データを取得してその国のGDPを集計したり、
データセット全体に対して集計を行ったりすることもできます：
*)

// スロヴァキアの10年単位の平均値を計算します
decades?``Slovak Republic`` |> Stats.levelMean fst

// 全国の10年単位の平均値を計算します
decades
|> Frame.getNumericColumns 
|> Series.mapValues (Stats.levelMean fst)
|> Frame.ofColumns

// 10年単位の標準偏差をUSドル基準で計算します
decades?Belgium * 1.0e9 
|> Stats.levelStdDev fst

(**
これまでは階層的インデックスを1つしか持たないようなデータフレームを
対象にしてきました。
しかし行と列の両方に階層的インデックスがある場合でも完璧に処理できます。
以下のスニペットではGDP平均を基準にして国をグループ化しています
(さらに行を10年単位にグループ化しています)：
*)

// GDPを500ビリオン(5,000億)ドルと比較して国をグループ化します
let byGDP = 
  decades |> Frame.transpose |> Frame.groupRowsUsing (fun k v -> 
    v.As<float>() |> Stats.mean > 500.0)
(**
(`byGDP` 上にマウスを移動させると) 2つの階層が型に含まれていることが確認できます。
列キーは `bool * string` (豊かどうかと国名)で、
行キーは `string * int` (10年単位および年)です。
このコードでは2つの列グループが作成されます。
1つはフランス(France)、ドイツ(Germany)、イタリア(Italy)を含む列で、
もう1つの列にはそれ以外の国が含まれます。

集計機能は(直接的には)行に対してのみサポートされますが、
`Frame.transpose` を使用すれば行と列を入れ替えることができます。

<a name="missing"></a>
値無しへの対処
--------------

値無しに対するサポートは組み込みで用意されています。
つまりシリーズやフレームはいずれも値無しを含むことができます。
データからシリーズまたはフレームを構成する際に、
特定の値が自動的に「値無し」として処理されます。
具体的には `Double.NaN` や、参照型およびnull許容型における `null` が該当します：
*)
(*** define-output:misv1 ***)
Series.ofValues [ Double.NaN; 1.0; 3.14 ]

(*** include-it:misv1 ***)

(*** define-output:misv2 ***)
[ Nullable(1); Nullable(); Nullable(3) ]
|> Series.ofValues

(*** include-it:misv2 ***)

(**
`Series.mean` のような統計計算を行う場合、値無しは自動的にスキップされます。
また `Series.mapValues` のように、射影やフィルタリングを行う際にも無視されます。
値無しを処理したい場合には `Series.mapAll` を使用します。
この場合には値が `option<T>` として取得できます
(ここでは [先のセクション](#creating-csv) で使用したサンプルデータセットを使用します)：
*)

// 値無しを含む列を取得します
let ozone = air?Ozone 

// 値無しを0に置き換えます
ozone |> Series.mapAll (fun k v -> 
  match v with None -> Some 0.0 | v -> v)

(**
実際、 `Series` モジュールには値無しを
より手軽に埋めるための関数が定義されているため、
`Series.mapAll` を使用する必要はほとんどないでしょう：
*)

// 値無しを定数値で置き換えます
ozone |> Series.fillMissingWith 0.0

// 値無しではない値が前方にコピーされて
// 値無しが埋められます
ozone |> Series.fillMissing Direction.Backward

// 値無しではない値が後方にコピーされます
// (1番目が値無しの場合にはそのまま値無しです！)
ozone |> Series.fillMissing Direction.Forward

// 値無しを埋めて、埋められなかったものを除外します
ozone |> Series.fillMissing Direction.Forward
      |> Series.dropMissing

(**
上記以外による値無しへの対処方法についてはライブラリでは直接サポートしていませんが、
`Series.fillMissingUsing` を使用すれば簡単に機能を追加できます。
この関数はすべての値無しに対して呼ばれることになる関数を引数にとります。
たとえば補完関数が既にあるとして、この関数を `fillMissingUsing` に指定すれば
必要になる補完処理を実行することができます。

たとえば以下のスニペットでは(値無しではない)前後の値の平均か、
どちらかの値(あるいは前後が値無しの場合には0)を返すようにしています：
*)

// 補完関数を使用して値無しを置き換えます
ozone |> Series.fillMissingUsing (fun k -> 
  // 前後の値を取得します
  let prev = ozone.TryGet(k, Lookup.ExactOrSmaller)
  let next = ozone.TryGet(k, Lookup.ExactOrGreater)
  // 利用可能な値に応じたパターンマッチ
  match prev, next with 
  | OptionalValue.Present(p), OptionalValue.Present(n) -> 
      (p + n) / 2.0
  | OptionalValue.Present(v), _ 
  | _, OptionalValue.Present(v) -> v
  | _ -> 0.0)
