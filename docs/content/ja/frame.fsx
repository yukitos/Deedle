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
(詳細については[高度なインデックシングの節](#indexing)を参照)。
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
(これは [先の節で](#creating-recd) F#のレコードから作成したものです)：

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
The previous examples were always looking for an exact key. If we have an ordered
series, we can search for a nearest available key and we can also perform slicing.
We use MSFT stock prices [from earlier example](#creating-csv):
*)

// Get series with opening prices
let opens = msft?Open

// Fails. The key is not available in the series
try opens.[DateTime(2013, 1, 1)] with e -> nan
// Works. Find value for the nearest greater key
opens.Get(DateTime(2013, 1, 1), Lookup.ExactOrSmaller)
// Works. Find value for the nearest smaler key
opens.Get(DateTime(2013, 1, 1), Lookup.ExactOrSmaller)

(**
When using instance members, we can use `Get` which has an overload taking
`Lookup`. The same functionality is exposed using `Series.lookup`. We can
also obtain values for a sequence of keys:
*)
// Find value for the nearest greater key
opens |> Series.lookup (DateTime(2013, 1, 1)) Lookup.ExactOrGreater

// Get first price for each month in 2012
let dates = [ for m in 1 .. 12 -> DateTime(2012, m, 1) ]
opens |> Series.lookupAll dates Lookup.ExactOrGreater

(**
With ordered series, we can use slicing to get a sub-range of a series:
*)

(*** define-output:opens ***)
opens.[DateTime(2013, 1, 1) .. DateTime(2013, 1, 31)]
|> Series.mapKeys (fun k -> k.ToShortDateString())

(*** include-it:opens ***)

(** 
The slicing works even if the keys are not available in the series. The lookup
automatically uses nearest greater lower bound and nearest smaller upper bound
(here, we have no value for January 1).

Several other options - discussed in [a later section](#indexing) - are available when using
hierarchical (or multi-level) indices. But first, we need to look at grouping.

<a name="grouping"></a>
Grouping data
-------------

Grouping of data can be performed on both unordered and ordered series and frames.
For ordered series, more options (such as floating window or grouping of consecutive
elements) are available - these can be found in the [time series tutorial](series.html).
There are essentially two options: 

 - You can group series of any values and get a series of series (representing individual 
   groups). The result can easily be turned into a data frame using `Frame.ofColumns` or
   `Frame.ofRows`, but this is not done automatically.

 - You can group a frame rows using values in a specified column, or using a function.
   The result is a frame with multi-level (hierarchical) index. Hierarchical indexing
   [is discussed later](#indexing).

Keep in mind that you can easily get a series of rows or a series of columns from a frame
using `df.Rows` and `df.Columns`, so the first option is also useful on data frames.

### Grouping series

In the following sample, we use the data frame `people` loaded from F# records in 
[an earlier section](#creating-recd). Let's first get the data:
*)
let travels = people.GetColumn<string list>("Countries")
// [fsi:val travels : Series<string,string list> =]
// [fsi:  Joe     -> [UK; US; UK]       ]
// [fsi:  Tomas   -> [CZ; UK; US; ... ] ]
// [fsi:  Eve     -> [FR] ]              
// [fsi:  Suzanne -> [US]]
(**
Now we can group the elements using both key (e.g. length of a name) and using the
value (e.g. the number of visited countries):
*)
// Group by name length (ignoring visited countries)
travels |> Series.groupBy (fun k v -> k.Length)
// Group by visited countries (people visited/not visited US)
travels |> Series.groupBy (fun k v -> List.exists ((=) "US") v)

// Group by name length and get number of values in each group
travels |> Series.groupInto 
  (fun k v -> k.Length) 
  (fun len people -> Series.countKeys people)
(**
The `groupBy` function returns a series of series (series with new keys, containing
series with all values for a given new key). You can than transform the values using
`Series.mapValues`. However, if you want to avoid allocating all intermediate series,
you can also use `Series.groupInto` which takes projection function as a second argument.
In the above examples, we count the number of keys in each group.

As a final example, let's say that we want to build a data frame that contains individual
people (as rows), all countries that appear in someone's travel list (as columns). 
The frame contains the number of visits to each country by each person:
*)
(*** define-output: trav ***)
travels
|> Series.mapValues (Seq.countBy id >> series)
|> Frame.ofRows
|> Frame.fillMissingWith 0

(*** include-it: trav ***)

(**
The problem can be solved just using `Series.mapValues`, together with standard F#
`Seq` functions. We iterate over all rows (people and their countries). For each
country list, we generate a series that contains individual countries and the count
of visits (this is done by composing `Seq.countBy` and a function `series` to build
a series of observations). Then we turn the result to a data frame and fill missing
values with the constant zero (see a section about [handling missing values](#missing)).

### Grouping data frames

So far, we worked with series and series of series (which can be turned into data frames
using `Frame.ofRows` and `Frame.ofColumns`). Next, we look at working with data frames.

Assume we loaded [Titanic data set](http://www.kaggle.com/c/titanic-gettingStarted) 
that is also used on the [project home page](index.html). First, let's look at basic
grouping (also used in the home page demo):
*)

// Group using column 'Sex' of type 'string'
titanic |> Frame.groupRowsByString "Sex"

// Grouping using column converted to 'decimal'
let byDecimal : Frame<decimal * _, _> = 
  titanic |> Frame.groupRowsBy "Fare"

// This is easier using member syntax
titanic.GroupRowsBy<decimal>("Fare")

// Group using calculated value - length of name
titanic |> Frame.groupRowsUsing (fun k row -> 
  row.GetAs<string>("Name").Length)

(**
When working with frames, you can group data using both rows and columns. For most
functions there is `groupRows` and `groupCols` equivalent.
The easiest functions to use are `Frame.groupRowsByXyz` where `Xyz` specifies the 
type of the column that we're using for grouping. For example, we can easily group
rows using the "Sex" column.

When using less common type, you need to specify the type of the column. You can 
see this on lines 5 and 9 where we use `decimal` as the key. Finally, you can also
specify key selector as a function. The function gets the original key and the row
as a value of `ObjectSeries<K>`. The type has various members for getting individual
values (columns) such as `GetAs` which allows us to get a column of a specified type.

### Grouping by single key

A grouped data frame uses multi-level index. This means that the index is a tuple
of keys that represent multiple levels. For example:
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
As you can see, the pretty printer understands multi-level indices and 
outputs the first level (sex) followed by the second level (passanger id).
You can turn frame with two-level index into a series of data frames
(and vice versa) using `Frame.unnest` and `Frame.nest`:
*)
let bySex = titanic |> Frame.groupRowsByString "Sex" 
// Returns series with two frames as values
let bySex1 = bySex |> Frame.nest
// Converts unstacked data back to a single frame
let bySex2 = bySex |> Frame.nest |> Frame.unnest
(**

### Grouping by multiple keys
Finally, we can also apply grouping operation repeatedly to group data using
multiple keys (and get a frame indexed by more than 2 levels). For example,
we can group passangers by their class and port where they embarked:
*)
// Group by passanger class and port
let byClassAndPort = 
  titanic
  |> Frame.groupRowsByInt "Pclass"
  |> Frame.groupRowsByString "Embarked"
  |> Frame.mapRowKeys Pair.flatten3

// Get just the Age series with the same row index
let ageByClassAndPort = byClassAndPort?Age
(**
If you look at the type of `byClassAndPort`, you can see that it is
`Frame<(string * int * int),string>`. The row key is a tripple consisting
of port identifier (string), passanger class (int between 1 and 3) and the
passanger id. The multi-level indexing is preserved when we get a single
series from the frame.

As our last example, we look at various ways of aggregating the groups:
*)
// Get average ages in each group
byClassAndPort?Age
|> Stats.levelMean Pair.get1And2Of3

// Averages for all numeric columns
byClassAndPort
|> Frame.getNumericColumns
|> Series.dropMissing
|> Series.mapValues (Stats.levelMean Pair.get1And2Of3)
|> Frame.ofColumns

// Count number of survivors in each group
byClassAndPort.GetColumn<bool>("Survived")
|> Series.applyLevel Pair.get1And2Of3 (Series.values >> Seq.countBy id >> series)
|> Frame.ofRows

(**
The second snippet combines a number of useful functions. It uses `Frame.getNumericColumns`
to obtain just numerical columns from a data frame. Then it drops the non-numerical columns
using `Series.dropMissing`. Then we use `Series.mapValues` to apply the averaging operation
to all columns.

The last snippet is alo interesting. We get the "Survived" column (which 
contains Boolean values) and we aggregate each group using a specified function.
The function is composed from three components - it first gets the values in the
group, counts them (to get a number of `true` and `false` values) and then creates
a series with the results. The result looks as the following table (some values
were omitted):

             True  False     
    C 1  ->  59    26        
      2  ->  9     8         
      3  ->  25    41        
    S 1  ->  74    53        
      2  ->  76    88        
      3  ->  67    286      
                  

<a name="pivot"></a>
Summarizing data with pivot table
---------------------------------

In the previous section, we looked at grouping, which is a very general 
data manipulation operation. However, very often we want to perform two operations
at the same time - group the data by certain keys and produce an aggregate. This
combination is captured by the concept of a _pivot table_. 

A pivot table is a useful tool if you want to summarize data in the frame based
on two keys that are available in the rows of the data frame. 

For example, given the titanic data set that [we loaded earlier](#creating-csv) and
explored in the previous section, we might want to compare the survival rate for males 
and females. The pivot table makes this possible using just a single call:
*)

(*** define-output:pivot1 ***)
titanic 
|> Frame.pivotTable 
    // Returns a new row key
    (fun k r -> r.GetAs<string>("Sex")) 
    // Returns a new column key
    (fun k r -> r.GetAs<bool>("Survived")) 
    // Specifies aggregation for sub-frames
    Frame.countRows 
(**
The `pivotTable` function (and the corresponding `PivotTable` method) take three arguments.
The first two specify functions that, given a row in the original frame, return a new
row key and column key, respectively. In the above example, the new row key is
the `Sex` value and the new column key is whether a person survived or not. As a result
we get the following two by two table:
*)

(*** include-it:pivot1 ***)

(**
The pivot table operation takes the source frame, partitions the data (rows) based on the 
new row and column keys and then aggregates each frame using the specified aggregation. In the
above example, we used `Frame.countRows` to simply return number of people in each sub-group.
However, we could easily calculate other statistic - such as average age:
*) 

(*** define-output:pivot2 ***)
titanic 
|> Frame.pivotTable 
    (fun k r -> r.GetAs<string>("Sex")) 
    (fun k r -> r.GetAs<bool>("Survived")) 
    (fun frame -> frame?Age |> Stats.mean)
|> round
(**
The results suggest that older males were less likely survive than younger males, but 
older females were more likely to survive then younger females:
*)

(*** include-it:pivot2 ***)

(**
<a name="indexing"></a>
Hierarchical indexing
---------------------

For some data sets, the index is not a simple sequence of keys, but instead a more
complex hierarchy. This can be captured using hierarchical indices. They also provide
a convenient way of dealing with multi-dimensional data. The most common source
of multi-level indices is grouping (the previous section has a number of examples).

### Lookup in the World Bank data set

In this section, we start by looking at the [World Bank data set from earlier](#creating-wb).
It is a data frame with two-level hierarchy of columns, where the first level is the name
of region and the second level is the name of country.

Basic lookup can be performed using slicing operators. The following are only available 
in F# 3.1:
*)

// Get all countries in Euro area
world.Columns.["Euro area", *]
// Get Belgium data from Euro area group
world.Columns.[("Euro area", "Belgium")]
// Belgium is returned twice - from both Euro and OECD
world.Columns.[*, "Belgium"]

(**
In F# 3.0, you can use a family of helper functions `LookupXOfY` as follows:
*)

// Get all countries in Euro area
world.Columns.[Lookup1Of2 "Euro area"]
// Belgium is returned twice - from both Euro and OECD
world.Columns.[Lookup2Of2 "Belgium"]

(**
The lookup operations always return data frame of the same type as the original frame.
This means that even if you select one sub-group, you get back a frame with the same
multi-level hierarchy of keys. This can be easily changed using projection on keys:
*)
// Drop the first level of keys (and get just countries)
let euro = 
  world.Columns.["Euro area", *]
  |> Frame.mapColKeys snd

(**
### Grouping and aggregating World Bank data

Hierarchical keys are often created as a result of grouping. For example, we can group
the rows (representing individual years) in the Euro zone data set by decades
(for more information about grouping see also [grouping section](#grouping) in this
document).

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
Now that we have a data frame with hierarchical index, we can select data
in a single group, such as 1990s. The result is a data frame of the same type.
We can also multiply the values, to get original GDP in USD (rather than billions):
*)
decades.Rows.["1990s", *] * 1e9

(**
The `Frame` and `Series` modules provide a number of functions for aggregating the
groups. We can access a specific country and aggregate GDP for a country, or we can
apply aggregation to the entire data set:
*)

// Calculate means per decades for Slovakia
decades?``Slovak Republic`` |> Stats.levelMean fst

// Calculate means per decateds for all countries
decades
|> Frame.getNumericColumns 
|> Series.mapValues (Stats.levelMean fst)
|> Frame.ofColumns

// Calculate standard deviation per decades in USD
decades?Belgium * 1.0e9 
|> Stats.levelStdDev fst

(**
So far, we were working with data frames that only had one hierarchical index. However,
it is perfectly possible to have hierarchical index for both rows and columns. The following
snippet groups countries by their average GDP (in addition to grouping rows by decades):
*)

// Group countries by comparing average GDP with $500bn
let byGDP = 
  decades |> Frame.transpose |> Frame.groupRowsUsing (fun k v -> 
    v.As<float>() |> Stats.mean > 500.0)
(**
You can see (by hovering over `byGDP`) that the two hierarchies are captured in the type.
The column key is `bool * string` (rich? and name) and the row key is `string * int` 
(decade, year). This creates two groups of columns. One containing France, Germany and
Italy and the other containing remaining countries.

The aggregations are only (directly) supported on rows, but we can use `Frame.transpose`
to switch between rows and columns. 

<a name="missing"></a>
Handling missing values
-----------------------

THe support for missing values is built-in, which means that any series or frame can
contain missing values. When constructing series or frames from data, certain values
are automatically treated as "missing values". This includes `Double.NaN`, `null` values
for reference types and for nullable types:
*)
(*** define-output:misv1 ***)
Series.ofValues [ Double.NaN; 1.0; 3.14 ]

(*** include-it:misv1 ***)

(*** define-output:misv2 ***)
[ Nullable(1); Nullable(); Nullable(3) ]
|> Series.ofValues

(*** include-it:misv2 ***)

(**
Missing values are automatically skipped when performing statistical computations such
as `Series.mean`. They are also ignored by projections and filtering, including
`Series.mapValues`. When you want to handle missing values, you can use `Series.mapAll` 
that gets the value as `option<T>` (we use sample data set from [earlier section](#creating-csv)):
*)

// Get column with missing values
let ozone = air?Ozone 

// Replace missing values with zeros
ozone |> Series.mapAll (fun k v -> 
  match v with None -> Some 0.0 | v -> v)

(**
In practice, you will not need to use `Series.mapAll` very often, because the
series module provides functions that fill missing values more easily:
*)

// Fill missing values with constant
ozone |> Series.fillMissingWith 0.0

// Available values are copied in backward 
// direction to fill missing values
ozone |> Series.fillMissing Direction.Backward

// Available values are propagated forward
// (if the first value is missing, it is not filled!)
ozone |> Series.fillMissing Direction.Forward

// Fill values and drop those that could not be filled
ozone |> Series.fillMissing Direction.Forward
      |> Series.dropMissing

(**
Various other strategies for handling missing values are not currently directly 
supported by the library, but can be easily added using `Series.fillMissingUsing`.
It takes a function and calls it on all missing values. If we have an interpolation
function, then we can pass it to `fillMissingUsing` and perform any interpolation 
needed.

For example, the following snippet gets the previous and next values and averages
them (if they are available) or returns one of them (or zero if there are no values
at all):
*)

// Fill missing values using interpolation function
ozone |> Series.fillMissingUsing (fun k -> 
  // Get previous and next values
  let prev = ozone.TryGet(k, Lookup.ExactOrSmaller)
  let next = ozone.TryGet(k, Lookup.ExactOrGreater)
  // Pattern match to check which values were available
  match prev, next with 
  | OptionalValue.Present(p), OptionalValue.Present(n) -> 
      (p + n) / 2.0
  | OptionalValue.Present(v), _ 
  | _, OptionalValue.Present(v) -> v
  | _ -> 0.0)
