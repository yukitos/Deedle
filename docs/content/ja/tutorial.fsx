(*** hide ***)
#I "../../../bin/"

(**
F#を使用して10分で学ぶDeedle
============================

このドキュメントでは、F#のデータフレームライブラリにおける主要な機能の概要を説明します。
なおこのページをGitHubから [F# スクリプトファイル](https://github.com/BlueMountainCapital/Deedle/blob/master/docs/content/tutorial.fsx)
としてダウンロードすれば、サンプルをインタラクティブに実行することもできます。

最初の手順として、 [NuGet経由で](https://www.nuget.org/packages/Deedle) `Deedle.dll` をインストールします。
次にライブラリをロードします。
F# Interactive上でライブラリの `.dll` をロードしている `.fsx` ファイルをロードすることにより、
データフレームやシリーズデータを表す型に対する簡易プリンターを登録できます。
今回のサンプルでは [F# Charting](http://fsharp.github.io/FSharp.Charting) も必要になるため、
以下のようにします：

*)
#I "../../../packages/FSharp.Charting.0.90.6"
#I "../../../packages/Deedle.0.9.5"
#load "FSharp.Charting.fsx"
#load "Deedle.fsx"

open System
open Deedle
open FSharp.Charting

(**
<a name="creating"></a>

シリーズおよびフレームの作成
----------------------------

データフレームは一意な列名を持ったシリーズのコレクションです
(列名は実際には文字列である必要はありません)。
したがってデータフレームを作成するには、まず1つのシリーズを作成することになります：
*)

(*** define-output: create1 ***)
// キーとなるシーケンスと値のシーケンスを作成します
let dates  = 
  [ DateTime(2013,1,1); 
    DateTime(2013,1,4); 
    DateTime(2013,1,8) ]
let values = 
  [ 10.0; 20.0; 30.0 ]
let first = Series(dates, values)

// 観測対象となる1つのリストからシリーズを作成します
Series.ofObservations
  [ DateTime(2013,1,1) => 10.0
    DateTime(2013,1,4) => 20.0
    DateTime(2013,1,8) => 30.0 ]

(*** include-it: create1 ***)

(*** define-output: create2 ***)
// 'Series.ofObservations' の省略バージョンです
series [ 1 => 1.0; 2 => 2.0 ]

// キーを明示的(かつ順番通り)に指定してシリーズを作成します
Series.ofValues [ 10.0; 20.0; 30.0 ]
(*** include-it: create2 ***)

(**
シリーズの型はジェネリックである点に注意してください。
`Series<K, T>` はキーの型が `K` で、値の型が `T` です。
ではランダムな値を持った10日分のシリーズデータを生成してみましょう：
*)

/// 'first' から 'count' 日分の日付を生成します
let dateRange (first:System.DateTime) count = (*[omit:(...)]*)
  seq { for i in 0 .. (count - 1) -> first.AddDays(float i) }(*[/omit]*)

/// ランダムなdouble値を 'count' 個生成します
let rand count = (*[omit:(...)]*)
  let rnd = System.Random()
  seq { for i in 0 .. (count - 1) -> rnd.NextDouble() }(*[/omit]*)

// 10日分の値を持ったシリーズ
let second = Series(dateRange (DateTime(2013,1,1)) 10, rand 10)

(*** include-value: (round (second*100.0))/100.0 ***)

(**
そうすると `first` と `second` という2つの列を持ち、
それぞれが列と同名の値を持つような
データフレームを簡単に作成できるようになります：
*)

let df1 = Frame(["first"; "second"], [first; second])

(*** include-value: df1 ***)

(** 
データフレームを表す型 `Frame<TRowKey, TColumnKey>` には2つのジェネリック引数があります。
1つめの引数は行キーを表す型で、先ほどの例では `DateTime` を明示的に指定しましたが、
指定しない場合には`int` 型になります。
2つめの引数は列キーの型です。
一般的には `string` ですが、日付を列キーにした転置バージョンのフレームを作成すると
便利な場合もあるでしょう。
データフレームには異種データを同梱させることができるため、値の型がない場合もあります。
この場合にはデータフレームからデータを取得する際に型を指定する必要があります。

出力結果からわかるように、フレームを作成すると2つのシリーズのインデックスが
自動的に結合されます(すべてのシリーズに含まれるすべての日付が結果に含まれるよう、
「外部結合(outer join)」が行われます)。
データフレームの `first` 列にはいくつか値無しのデータが含まれています。

また、さらに手軽なシンタックスを使って行データから、あるいは個別のデータから
フレームを作成することもできます：
*)

// 先と同じ
let df2 = Frame.ofColumns ["first" => first; "second" => second]

// 転置バージョン。ここでは行に"first"と"second"、列に日付が設定されます
let df3 = Frame.ofRows ["first" => first; "second" => second]

// 個別の値 (行 * 列 * 値) を指定してフレームを作成します
let df4 = 
  [ ("Monday", "Tomas", 1.0); ("Tuesday", "Adam", 2.1)
    ("Tuesday", "Tomas", 4.0); ("Wednesday", "Tomas", -5.4) ]
  |> Frame.ofValues

(**
データフレームはF#のレコード型(あるいはpublicで読み取り可能なプロパティを持った任意のクラス)の
コレクションからも簡単に作成できます。
`Frame.ofRecords` 関数を使用すると、レコードの名前とプロパティの型をリフレクションで探し出して、
同じ構造を持ったデータフレームを作成することができます。

*)
// 'Price' というレコードと 'prices' コレクションがあるとします
type Price = { Day : DateTime; Open : float }
let prices = 
  [ { Day = DateTime.Now; Open = 10.1 }
    { Day = DateTime.Now.AddDays(1.0); Open = 15.1 }
    { Day = DateTime.Now.AddDays(2.0); Open = 9.1 } ]

// 'Day' と 'Open' という列を持ったデータフレームを作成します
let df5 = Frame.ofRecords prices

(**
最後に、データフレームはCSVから読み取ることもできます：
*)
let msftCsv = Frame.ReadCsv(__SOURCE_DIRECTORY__ + "/../data/stocks/MSFT.csv")
let fbCsv = Frame.ReadCsv(__SOURCE_DIRECTORY__ + "/../data/stocks/FB.csv")

(*** include-value: fbCsv ***)

(**
データフレームはデータのロード時に値を解析して、
その値に最適な型を自動判別します。
しかし日付と時刻については自動変換は行われません。
どの日付型(`DateTime` や `DateTimeOffset` あるいは他のカスタム型)による表現が適切なのかを
ユーザーが決める必要があります。

<a name="reindexing-and-joins"></a>

インデックスと結合を指定する
----------------------------

ここまでで株価を含んだ `fbCsv` と `msftCsv` フレームを用意出来ましたが、
これらは数値順にインデックスされています。
つまりたとえば4番目の価格を取得するといったことができます。
しかしここでは日付順で並び替えようと思っています
(いくつか値無しのものが出てくるでしょう)。
そのためには行のインデックスを "Date" 列に設定します。
日付をインデックスに設定した後はインデックス順に並び替える必要があります。
Yahoo Financeの価格情報は新しいものから古いものの順で表示されていますが、
今回のデータフレームでは古いものから新しいものの昇順で表示させることにします。

フレームには順序付きインデックスがあるため、後で必要になる機能を追加しておきます
(たとえばインデックスとして明示的に含まれていないような日付を指定して
部分範囲を選択できるようにします)。

*)

// インデックスならびに行の順番としてDate列を使用します
let msftOrd = 
  msftCsv
  |> Frame.indexRowsDate "Date"
  |> Frame.sortRowsByKey

(**
`indexRowsDate` 関数は `DateTime` 型の行を新しいインデックスとして使用します。
ライブラリには他にも一般的な型のインデックスに対する関数(たとえば `indexRowsInt`)や、
ジェネリック関数もあります。
ジェネリック関数を使用する場合、型アノテーションが必要になるため、
特定の型に対する関数を使用したほうがよいでしょう。
次は `Frame` モジュールにある別の関数を使用して行をソートします。
このモジュールにはどのような状況でも使用できるような、便利な関数が多数定義されています。
サポートされている機能を確認するために、関数のリストに目を通しておくことをおすすめします。

さてこれで正しくインデックスが付けられた株価が用意できたので、
関心のあるデータ(始値と終値)だけを含む新しいデータフレームを作成して、
それらの差分を表す新しい列を追加します：
*)

(*** define-output: plot1 ***)
// 始値(Open)と終値(Close)だけを含んだデータフレームを作成します
let msft = msftOrd.Columns.[ ["Open"; "Close"] ]

// 始値と終値の差分を含む新しい列を作成します
msft?Difference <- msft?Open - msft?Close

// Facebookデータに対しても同じ処理を行います
let fb = 
  fbCsv
  |> Frame.indexRowsDate "Date"
  |> Frame.sortRowsByKey
  |> Frame.sliceCols ["Open"; "Close"]
fb?Difference <- fb?Open - fb?Close

// これで差分を簡単にプロットできるようになります
Chart.Combine
  [ Chart.Line(msft?Difference |> Series.observations) 
    Chart.Line(fb?Difference |> Series.observations) ]

(*** include-it:plot1 ***)

(**
`f.Columns.[ .. ]` として列を選択すると、(既に行ったように)列のリスト、
あるいは単一の列キー、あるいは(関連づけられたインデックスが順序を持つのであれば)
単一の範囲として扱うことができます。
そして `df?Column <- (...)` というシンタックスを使用すると、
データフレームに新しい列を追加できます。
これはデータフレームでサポートされている唯一の可変操作です。
その他の操作では新規作成されたデータフレームが結果として返されます。

次にMicrosoftとFacebook両方の(適切にアラインされた)データを含む
単一のデータフレームを作成します。
そのためには `Join` メソッドを使用します。
ただしその前に、キーの重複は許容されていないため、それぞれの列名を変更します：
*)

(*** define-output:msfb ***)
// 列名が一意になるように変更します
let msftNames = ["MsftOpen"; "MsftClose"; "MsftDiff"]
let msftRen = msft |> Frame.indexColsWith msftNames

let fbNames = ["FbOpen"; "FbClose"; "FbDiff"]
let fbRen = fb |> Frame.indexColsWith fbNames

// 外部結合 (値無しを含みつつ、アラインおよびフィルを行います)
let joinedOut = msftRen.Join(fbRen, kind=JoinKind.Outer)

// 内部結合 (値無しの行を削除します)
let joinedIn = msftRen.Join(fbRen, kind=JoinKind.Inner)

// 有効な値だけを対象にして、日ごとの差分を可視化します
Chart.Rows
  [ Chart.Line(joinedIn?MsftDiff |> Series.observations) 
    Chart.Line(joinedIn?FbDiff |> Series.observations) ]

(*** include-it:msfb ***)

(**
<a name="selecting"></a>

値の選択とスライシング
----------------------

データフレームにはデータアクセス時に使用できる主要なプロパティが2つあります。
`Rows` プロパティはそれぞれの行を(シリーズとして)含んだシリーズを返し、
`Columns` プロパティはそれぞれの列を(シリーズとして)含んだシリーズを返します。
これらのシリーズに対して様々な方法でインデックス化
あるいはスライシングを行うことができます：
*)

// 特定の日付の行を確認します
joinedIn.Rows.[DateTime(2013, 1, 2)]
// [fsi:val it : ObjectSeries<string> =]
// [fsi:  FbOpen    -> 28.00   ]
// [fsi:  FbClose   -> 27.44   ]
// [fsi:  FbDiff    -> -0.5599 ]
// [fsi:  MsftOpen  -> 27.62   ]
// [fsi:  MsftClose -> 27.25   ]
// [fsi:  MsftDiff  -> -0.3700 ]

// 2013年1月2日のFacebookの始値を取得します
joinedIn.Rows.[DateTime(2013, 1, 2)]?FbOpen
// [fsi:val it : float = 28.0]

(**

最初の式における返り値の型は `ObjectSeries<string>` で、
この型は `Series<string, obj>` から派生したもので、
型無しのシリーズを表します。
特定のキーに対する値を取得して、必要になる型に変換するためには
`GetAs<int>("FbOpen")` メソッド(または `TryGetAs` )を使用します。
型無しシリーズではデフォルトの `?` 演算子
(静的に既知の値型を使用して値を返す演算子)が隠ぺいされて、
代わりに任意の値を `float` へと自動変換する `?` 演算子が用意されます。

先の例では単一キーのインデクサを使用しました。
しかし複数キーを(リストとして)指定したり、
(スライシングの文法を使用して)範囲を指定したりすることもできます：
*)

// 2013年1月の月初め3日の値を取得します
let janDates = [ for d in 2 .. 4 -> DateTime(2013, 1, d) ]
let jan234 = joinedIn.Rows.[janDates]

// 3日間の始値の平均を計算します
jan234?MsftOpen |> Stats.mean

// 2013年1月全体の値を取得します
let jan = joinedIn.Rows.[DateTime(2013, 1, 1) .. DateTime(2013, 1, 31)] 

(*** include-value: Frame.map(round (jan*100.0))/100.0 |> Frame.mapRowKeys (fun dt -> dt.ToShortDateString()) ***)

// 月全体の平均を計算します
jan?FbOpen |> Stats.mean
jan?MsftOpen |> Stats.mean

(**
(先の例のように)単一の日付を指定した場合のインデックス演算子の結果は単一のデータシリーズ、
複数のインデックスや(今回の例のように)範囲を指定した場合の結果は新しいデータフレームになります。

今回使用した `Series` モジュールには、 `mean` `sdv` `sum` など、
データシリーズに対する便利な統計用関数が定義されています。

なお範囲を指定したスライシング(2番目の例)では
1月1日から31日までの日付シーケンスが実際に生成されているわけではない点に注意してください。
これら2つの日付がインデックスとして渡されているだけです。
データフレームには順序付きインデックスがあるため、
1月1日よりも大きく、1月31日よりも小さいすべてのキーがインデックスによって検索されるというわけです
(ただしここには問題があります。
返されたデータフレームには1月1日のデータが含まれておらず、1月2日から始まっています。)

<a name="timeseries"></a>

時系列データを使用する
----------------------

既に説明したように、順序付きのシリーズまたはデータフレームがあれば、
様々な方法でデータを並び替えることができます。
先の例では厳密一致ではなく、下限上限を指定してスライシングしました。
同様に、ダイレクトルックアップを行う場合には
指定した値に最も近い小さな(あるいは大きな)要素を取得することもできます。

たとえば10日分10個のデータを持った2つのシリーズを用意します。
`daysSeries` は `DateTime.Today` (午前 12:00) を始点とするキーを持ち、
`obsSeries` は 現在の時刻が設定された日付をキーに持つようにします
(これは間違った表現ですが、アイディアとしては伝わるはずです)：
*)

let daysSeries = Series(dateRange DateTime.Today 10, rand 10)
let obsSeries = Series(dateRange DateTime.Now 10, rand 10)

(*** include-value: (round (daysSeries*100.0))/100.0 ***)
(*** include-value: (round (obsSeries*100.0))/100.0 ***)

(**
`daysSeries.[date]` というインデックス演算子は **厳密な** 意味を持つため、
正確な日付が参照できない場合にはエラーになります。
一方、 `Get` メソッドには要求された動作を指定するための引数があります：
*)

// 現在の時刻に対応するデータは無いのでエラーになります。
try daysSeries.[DateTime.Now] with _ -> nan
try obsSeries.[DateTime.Now] with _ -> nan

// 動作します。DateTime.Today (12:00 AM)に対応する値が取得できます。
daysSeries.Get(DateTime.Now, Lookup.ExactOrSmaller)
// 動作しません。Today (12:00 AM) 以前で最も近いキーは存在しません。
try obsSeries.Get(DateTime.Today, Lookup.ExactOrSmaller)
with _ -> nan

(**
(option値を取得する) `TryGet` あるいは
(1度に複数のキーを指定してルックアップを行う) `GetItems` を呼ぶ場合にも、
同じように動作を指定できます。
なおこの動作は順序付きインデックスを持ったシリーズまたはフレームに対してのみ
有効である点に注意してください。
順序が無い場合はすべての操作が厳密一致になります。

データフレームをleft joinまたはright joinする場合にも指定できます。
デモとして、1と2というインデックスをそれぞれ持った
2つのデータフレームを用意してみます：
*)

let daysFrame = [ 1 => daysSeries ] |> Frame.ofColumns
let obsFrame = [ 2 => obsSeries ] |> Frame.ofColumns

// 列2にあるすべての値は(一致する時間が無いため)値無しです
let obsDaysExact = daysFrame.Join(obsFrame, kind=JoinKind.Left)

// すべての値が有効です。
// 各日付において、やや経過した時間に対して最も近い小さな値をキーとする値を取得しています。
let obsDaysPrev = 
  (daysFrame, obsFrame) 
  ||> Frame.joinAlign JoinKind.Left Lookup.ExactOrSmaller

// 1番目の値は値無しですが、2番目以降は有効な値です
// (1番目のデータは最も小さなキーであるため、
// それよりも最も近くて大きい値は存在しません)。
let obsDaysNext =
  (daysFrame, obsFrame) 
  ||> Frame.joinAlign JoinKind.Left Lookup.ExactOrGreater

(**
一般的に、 `Series` や `Frame` モジュール内の関数を使用して行えることは
いずれもオブジェクトのメンバー(あるいは拡張メンバー)を使用しても
行うことができるようになっています。
先の例では両方を使用しました。
まずオプション引数を指定した `Join` をメンバーメソッドとして呼び出した後、
`joinAlign` 関数を呼び出しました。
好みに応じてどちらを使用しても構いません。
今回は(ページに収まらないような長い式を記述するのではなく)
コードをパイプライン化したかったので `joinAlign` を使用しました

`Join` メソッドには2つのオプション引数を指定できます。
引数 `?lookup` は `?kind` が `Left` と `Right` の
いずれでもない場合には無視されます。
また、データフレームに順序が無い場合には厳密一致のデフォルト動作になります。
`joinAlign` 関数も同様です。

<a name="projections"></a>

射影とフィルタリング
--------------------

シリーズに対するフィルタリング(filtering)と射影(projection)は
それぞれ `Where` と `Select` メソッドで行うことができます。
またこれらのメソッドには `Series.map` と `Series.filter` 関数が対応します。
(値またはキーのいずれか一方だけを対象に変換したい場合には
`Series.mapValues` や `Series.mapKeys` 関数も使用できます)。

これらのメソッドは直接データフレームに対して呼び出せないため、
(対象に応じて) `df.Rows` または `df.Columns` と記述する必要があります。
なお `Frame` モジュールにも `Frame.mapRows` という同じような関数があります。
以下のコードでは株価の高い方の名前
("FB"または"MSFT")を含んだ新しい列を追加しています：
*)

joinedOut?Comparison <- joinedOut |> Frame.mapRowValues (fun row -> 
  if row?MsftOpen > row?FbOpen then "MSFT" else "FB")

(**
行を射影またはフィルタリングする場合、値無しのデータに注意する必要があります。
行に対するアクセサー `row?MsftOpen` は特定の列の値を読み取ります
(そしてその値を`float`に変換します)が、列の値が無効な場合には
`MissingValueException` 例外がすろーされます。
`mapRowValues` のような射影関数ではこの例外が自動的にキャッチされて、
対応するシリーズの値が値無しだとマークされます
(ただしこれ以外の型の例外はキャッチされません)。

値無しに対する処理をより明示的に行う場合には `Series.hasAll ["MsfOpen"; "FbOpen"]`
として、必要な値がすべてシリーズにあるかどうかをチェックするようにします。
もし値が無いのであればラムダ関数で `null` を返すようにします。
そうするとそれが自動的に値無しだとみなされるようになります
(そして以降の操作で処理対象から外されることになるでしょう)。

さてこれでMicrosoftの株価がFacebookを上回った日数、
あるいはその逆の日数を取得できるようになりました：
*)

joinedOut.GetColumn<string>("Comparison")
|> Series.filterValues ((=) "MSFT") |> Series.countValues
// [fsi:val it : int = 220]

joinedOut.GetColumn<string>("Comparison")
|> Series.filterValues ((=) "FB") |> Series.countValues
// [fsi:val it : int = 103]

(**
この場合には、有効な行しか含まれなくなる
`joinedIn` を使用したほうがよかったかもしれません。
しかし値無しを含むデータフレームを処理することも多いため、
この方法を確認しておくことには意味があります。
別の方法も紹介しましょう：
*)

// 'Open'列だけを含むデータフレームを取得します
let joinedOpens = joinedOut.Columns.[ ["MsftOpen"; "FbOpen"] ]

// 値無しを含まない行だけを取得すれば
// 安全にフィルタおよびカウントできます
joinedOpens.RowsDense
|> Series.filterValues (fun row -> row?MsftOpen > row?FbOpen)
|> Series.countValues

(**
ポイントは6行目にある `RowsDense` の部分です。
これは `Rows` と同じような動作をしますが、
値無しを含まない行だけを返すという違いがあります。
したがって、チェックせずとも安全にフィルタリングを実行できるというわけです。

しかし `FbClose` 列は必要としていないため、
この列に値無しが含まれていても問題にはなりません。
そのため、元のデータフレームから必要な2つの列だけを射影して、
最初に `joinedOpens` を作成しているというわけです。

<a name="grouping"></a>

グループ化と集計
----------------

最後にグループ化(grouping)と集計(aggregation)について簡単に紹介します。
時系列データのグループ化に関する詳細については
[時系列機能のチュートリアル](series.html) を参照してください。
また、 [データフレームの機能](frame.html) には
順序無しのフレームに対するグループ化に関する説明があります。

ここでは `Frame.groupRowsUsing` という一番単純な機能を紹介します
(`GroupRowsUsing` メンバーメソッドもあります)。
この関数には各行における新しいキーを選択するキーセレクタを指定できます。
列内の値を使用してデータをグループ化したい場合には
`Frame.groupRowsBy column` というようにします。

以下のスニペットでは行を月および年でグループ化しています：
*)
let monthly =
  joinedIn
  |> Frame.groupRowsUsing (fun k _ -> DateTime(k.Year, k.Month, 1))

// [fsi:val monthly : Frame<(DateTime * DateTime),string> =]
// [fsi: ]
// [fsi:                        FbOpen  MsftOpen ]
// [fsi:  5/1/2012 5/18/2012 -> 38.23   29.27    ]
// [fsi:           5/21/2012 -> 34.03   29.75    ]
// [fsi:           5/22/2012 -> 31.00   29.76    ]
// [fsi:  :                     ...              ]
// [fsi:  8/1/2013 8/12/2013 -> 38.22   32.87    ]
// [fsi:           8/13/2013 -> 37.02   32.23    ]
// [fsi:           8/14/2013 -> 36.65   32.35    ]

(**
出力結果はページに収まるように省略してあります。
見ての通り、 `DateTime * DateTime` のタプルを行のキーとするような
データフレームが返されます。
このデータフレームは **階層的** (あるいはマルチレベル) インデックスとして
扱うことができます。
たとえば出力結果では(正しく順序づけられているとすれば)
自動的に複数行がグループとして表示されます

階層的インデックスに対しては様々な操作が可能です。
たとえば特定のグループ(2013年5月)に含まれる行を取得して、
グループ内の列の平均を計算することができます：
*)
monthly.Rows.[DateTime(2013,5,1), *] |> Stats.mean
// [fsi:val it : Series<string,float> =]
// [fsi:  FbOpen    -> 26.14 ]
// [fsi:  FbClose   -> 26.35 ]
// [fsi:  FbDiff    -> 0.20  ]
// [fsi:  MsftOpen  -> 33.95 ]
// [fsi:  MsftClose -> 33.76 ]
// [fsi:  MsftDiff  -> -0.19 ]

(**
このスニペットではF# 3.1 (Visual Studio 2013)以降で
利用可能なスライシング記法を使用しています。
以前のバージョンであれば
`monthly.Rows.[Lookup1Of2 (DateTime(2013,5,1))]` とすれば同じ動作になります。
これはキーの1番目だけを指定して、
2番目のコンポーネントは任意のものでよいということを示しています。
`Frame.getNumericColumns` と `Stats.levelMean` を組み合わせて
第1レベルの全グループの平均を取得することもできます：
*)
monthly 
|> Frame.getNumericColumns
|> Series.mapValues (Stats.levelMean fst)
|> Frame.ofColumns

(**
ここでは単にキーがタプルであることを利用しました。
`fst` 関数はキー(月および年)の1番目の日付を射影するため、
第1レベルのキーを含み、有効な数値列すべての平均値を持ったフレームが結果として返されます。
*)

