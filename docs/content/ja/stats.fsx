(*** hide ***)
#load "../../../bin/Deedle.fsx"
#load "../../../packages/FSharp.Charting.0.90.6/FSharp.Charting.fsx"
open System
open System.Globalization
open System.IO
open FSharp.Data
open Deedle
open FSharp.Charting
let root = __SOURCE_DIRECTORY__ + "/data/"

(**
フレームおよびシリーズの統計情報を計算する
==========================================

`Stats` 型にはシリーズやフレーム、さらにはシリーズでウィンドウを
移動あるいは拡張させつつ統計情報を高速に計算する関数が用意されています。
`Stats` 型にある標準統計関数にはオーバーロードがあり、
データフレームとシリーズの両方に対応しています。
さらに高度な機能についてはシリーズのみ対応しています
(ただし `Frame.getNumericCols` 関数と組み合わせることで
簡単に対応させることができます)。

<a name="stats"></a>
シリーズおよびフレームの統計情報
--------------------------------

このセクションでは、データフレームおよびシリーズ全体から
単純な統計情報を計算する方法を紹介します。
重要なポイントは値無しを扱う方法です。
そのため、ここでは値無しを含む大気質(air quality)のデータセットを
使用してデモを行います。
以下のスニペットでは `AirQuality.csv` をロードして、
`Ozone` 列にある値を表示しています：
*)
let air = Frame.ReadCsv(root + "AirQuality.csv", separators=";")
let ozone = air?Ozone
(*** include-value: ozone ***)

(**
### シリーズの統計情報

シリーズ `ozone` に対して `Stats` の様々な関数を使用することで
統計情報を計算できます。
以下の例では入力されたシリーズに対して、平均、最小値、最大値、中央値を含むような
(文字列によってインデックスされた)シリーズを作成しています：
*)

(*** define-output: ozinfo ***)
series [
  "Mean" => round (Stats.mean ozone)
  "Max" => Option.get (Stats.max ozone)
  "Min" => Option.get (Stats.min ozone)
  "Median" => Stats.median ozone ]

(*** include-it: ozinfo ***)

(**
出力結果を単純にするために、ここでは平均の値を丸めています
(実際には浮動小数点数です)。
なお値はシリーズ内で**利用可能な**値から計算されている点に注意してください。
統計用の関数はいずれも入力されたシリーズに含まれる値無しを無視します。

上の例にもある通り、`Stats.max` と `Stats.min` は単なる `float` ではなく、
`option<float>` を返します。
シリーズに値が含まれない場合には結果が `None` になります。
そのため、これらの関数は浮動小数点数だけでなく、整数値のシリーズや
他の型の値を含んだシリーズに対しても使用できます。
`Stats.mean` など、別の統計用関数では利用可能な値がない場合には `nan` が返されます。

### フレームの統計情報

`Stats.mean` など一部の関数はシリーズに対してだけでなく、
データフレーム全体に対して呼び出すことができます。
その場合、データフレームの各列に対して統計情報を計算した後、
`Series<'C, float>` が返されます。
なお `'C` は元のフレームの列キーです。

以下のスニペットでは `air` データセットのすべての列に対して平均および標準偏差を
計算した後、これら2つの列(シリーズ)の値を表示するためにフレームを作成しています：
*)
(*** define-output: airinfo ***)
let info = 
  [ "Mean" => Stats.mean air
    "+/-" => Stats.stdDev air ] |> frame
(*** include-value: round(info*100.0)/100.0 ***)

(**
シリーズの統計情報を計算した場合と同じく、値無しのデータは無視されます。
これが意図した動作ではない場合、
[Series モジュール](reference/deedle-seriesmodule.html)
にある関数を使用して、値無しを別の方法で処理することになります。

`Stats` モジュールには平均や標準偏差、分散といった基礎的な統計用関数がありますが、
他にも歪度や尖度といった高度な関数もあります。
すべての関数の一覧については、APIリファレンスの
[Series statistics](reference/deedle-stats.html#section5)
や
[Frame statistics](reference/deedle-stats.html#section1)
のセクションを参照してください。

<a name="moving"></a>
移動ウィンドウの統計情報
------------------------

`Stats` 型には移動ウィンドウの統計情報を効率的に取得する機能も実装されています。
この実装では各ウィンドウを個別に再計算する必要がない
オンラインアルゴリズムが採用されていますが、その代わりにインプットを走査するたびに
値が更新されるようになっています(そのため `Series.window` よりも高速です)。

移動ウィンドウの関数名には `moving` という接頭辞が付けられていて、
固定長のウィンドウを使用して移動統計を計算します。
以下の例では移動ウィンドウの長さ3で平均を計算しています：
*)
(*** define-output:mvmozone ***)
ozone
|> Stats.movingMean 3
(*** include-it:mvmozone ***)

(**
結果のシリーズのキーは入力したシリーズのキーと同じです。
ウィンドウのサイズ **n** における統計用移動関数
(カウント、総和、平均、分散、標準偏差、歪度、尖度)では、
最初の **n-1** 個の値が常に値無しになります
(つまりこれらは完全なウィンドウに対してのみ計算を行います)。
キー **1** の値が **N/A** になっているのはこのためです。
キー **2** の場合、ウィンドウ内で利用可能な値を使用して平均が計算されます。
つまり **(36+12)/2** です。

移動ウィンドウにおける最小値および最大値を計算する関数では動作が異なります。
最初の **n-1** 個の値が **N/A** になるのではなく、
より小さなウィンドウにおける極地が返されます：
*)
(*** define-output:mvxozone ***)
ozone
|> Stats.movingMin 3
(*** include-it:mvxozone ***)

(**
最初の値だけを含む1要素のウィンドウには値無ししか含まれていないため、
結果の最初の値は値無しになっています。
しかしキー **1** では(シリーズの先頭から数えて)2つの要素を含む
ウィンドウになっているため、値が計算されます。

### 注意

`Stats` 型のウィンドウ関数はウィンドウのサイズによって決まる固定サイズのウィンドウを
利用して効率的に計算を行います。
また、境界における挙動についても定型の機能が用意されています。
複雑なウィンドウ化(たとえばキー間の距離を元にしたウィンドウ)を行う場合や、
境界における挙動を変更したい場合、
あるいはチャンク化(隣接するチャンクを使用した計算)を行う場合には、
`Series` モジュールにある `Series.windowSizeInto` あるいは `Series.chunkSizeInto`
といったチャンク化あるいはウィンドウ化用の関数を使用します。
詳細についてはAPIリファレンスの
[Grouping, windowing and chunking](reference/deedle-seriesmodule.html#section1)
を参照してください。

<a name="exp"></a>
拡張ウィンドウ
--------------

拡張ウィンドウ (Expanding window) とは、シリーズの最初の時点では
1要素のウィンドウから始めて、シリーズ上を移動するたびに拡張されるような
ウィンドウのことです。
時間で順序づけられた時系列データの場合、
拡張ウィンドウを使用することによって
過去の既知の観測データをすべて使用して統計を計算できます。
言い換えると、現在のキーまですべての値を使用して統計が計算され、
ウィンドウの最後の位置にあるキーに結果が結びつけられます。
拡張ウィンドウ関数は `expanding` から始まる名前になっています。

以下のデモではOzoneシリーズに対して拡張平均および拡張標準偏差を計算しています。
結果のシリーズには元のシリーズと同じキーが含まれます。
なお結果を見やすくするために、
フレームを使用して2つのシリーズを整列しています：
*)
let exp =
  [ "Ozone" => ozone 
    "Mean" => Stats.expandingMean(ozone)
    "+/-" => Stats.expandingStdDev(ozone) ] |> frame
(*** include-value:(round(exp*100.0))/100.0 ***)

(**
この例からもわかるように、拡張ウィンドウ統計では
一般的に結果の冒頭部分にいくつか値無しが含まれます。
今回の場合は(1要素のウィンドウには値無ししか含まれないため)平均で最初の1つ、
(`stdDev` は2つ以上の値に対してのみ定義されるため)標準偏差で最初の2つが
値無しになっています。
唯一の例外は `expandingSum` で、値無しの総和は0になります。

<a name="multi"></a>
多層インデックス統計
--------------------

複数レベルの(階層的)インデックスを持つシリーズに対しては、
`level` という接頭辞を持つ関数を使用することによって、
1つのインデックスを対象にした統計処理を行うことができます。
複数レベルのインデックスを持つシリーズは(たとえば `'K1 * 'K2` のような)
タプルをキーとすることにより、直接作成できます。
あるいは `Frame.groupRowsBy` のようなグループ化操作を行って
作成することもできます。

たとえば月を1つめのキー、日を2つめのキーとするような、
2レベルのインデックスを持った時系列データを作成できます。
そして複数レベル用の(あるいはその他の)統計関数を使用することにより、
月毎の平均を個別に計算するといったことができます。

この例に対するデモが以下のコードです。
`air` データセットには5月から9月まで、日毎のデータが含まれています。
このデータセットに対して `Frame.indexRowsUsing` を呼び、
インデックスとしてタプルを返すようにしています：
*)
let dateFormat = CultureInfo.CurrentCulture.DateTimeFormat
let byMonth = air |> Frame.indexRowsUsing (fun r ->
    dateFormat.GetMonthName(r.GetAs("Month")), r.GetAs<int>("Day"))
(**
値 `byMonth` の型は `Frame<string * int, string>`で、
行インデックスが2レベルあることがわかります。
出力を少し見やすくするために、ここでは `GetMonthName` 関数を使用して
1レベル目のインデックスを月の名前に変換しています。

そして `level` の接頭辞を持った関数を使用することにより、
各列にアクセスして、1レベル目(つまりそれぞれの月)の統計情報を計算できます：
*)

(*** define-output:lvlozone ***)
byMonth?Ozone
|> Stats.levelMean fst
(*** include-it:lvlozone ***)

(**
現在のところ、`Stats` 型にはデータフレームの複数レベルに対して
統計関数を適用するような機能は実装されていません。
しかしこれは `Frame.getNumericCols` 関数と `Series.mapValues` を組み合わせるだけで
簡単に実装できます：
*)
(*** define-output:lvlall ***)
byMonth
|> Frame.sliceCols ["Ozone";"Solar.R";"Wind";"Temp"]
|> Frame.getNumericCols
|> Series.mapValues (Stats.levelMean fst)
|> Frame.ofRows
(*** include-it:lvlall ***)
(**
`Frame.getNumericCols` を直接呼び出すと"Day"および"Month"列の平均も
計算することになりますが、今回の例ではあまり意味がありません。
そのため、上のスニペットでは最初に `sliceCols` を呼び出して
関係のある列だけに絞り込んでいます。
*)
