(*** hide ***)
#nowarn "211"
#I "../../../packages/FSharp.Charting.0.90.6"
#I @"../../../bin"
open System
let airQuality = __SOURCE_DIRECTORY__ + "/../data/AirQuality.csv"

(**

RとDeedleの連携
===============

[R 型プロバイダー](http://bluemountaincapital.github.io/FSharpRProvider/)
を使用すると、RとF#とをスムーズに連携できるようになります。
R 型プロバイダーはインストール済みのパッケージを自動的に検出するため、
`RProvider` 名前空間経由でそれらにアクセスできるようになります。

F#用のR 型プロバイダーはRとF#の標準データ構造(数値や配列など)を自動的に変換します。
しかしこの変換機能は拡張可能であるため、その他のF#型に対する変換を
サポートすることもできます。

Deedleライブラリには、Deedleの ｀Frame<R, C>` と R の `data.frame`、
Deedleの `Series<K, V>` と
[zoo package](http://cran.r-project.org/web/packages/zoo/index.html)
(Z's ordered observations：Zの順序付き観測値)
を自動的に変換するような拡張機能が備えられています。

DeedleとR プロバイダーを組み合わせるには、
以下のNuGetパッケージをインストールするだけですみます
(このパッケージはR プロバイダーとDeedleに依存しているため、
追加でインストールするものはありません)。

<div class="row">
  <div class="span1"></div>
  <div class="span6">
    <div class="well well-small" id="nuget">
      F# DataFrame library は<a href="https://nuget.org/packages/Deedle.RPlugin">NuGet経由で</a>インストールできます：
      <pre>PM> Install-Package Deedle.RPlugin</pre>
    </div>
  </div>
  <div class="span1"></div>
</div>

このページではRとDeedle間でデータをやりとりする方法について簡単に説明します。
また、このページをGitHubから
[F# スクリプトファイル](https://github.com/BlueMountainCapital/Deedle/blob/master/docs/content/rinterop.fsx)
としてダウンロードすれば、サンプルをインタラクティブに実行できます。

<a name="setup"></a>

はじめに
--------

(「F# チュートリアル」のような)一般的なプロジェクトにおいて、
NuGetパッケージは `../packages` ディレクトリにインストールされます。
R プロバイダーとDeedleを使用するためには、
以下のようなコードを用意する必要があります：

> 訳注：インクルードディレクトリのパスは実際の環境、
  および使用しているライブラリのバージョンによって異なります。
  適宜修正してください。
*)
#I "../../../packages/Deedle.0.9.9-beta/"
#I "../../../packages/RProvider.1.0.7-alpha/"
#load "RProvider.fsx"
#load "Deedle.fsx"

open RProvider
open RDotNet
open Deedle
(**

Visual StudioのNuGetを使用していない場合には、
`Deedle.RPlugin` パッケージの `Deedle.RProvider.Plugin.dll` ファイルを
(`RProvider.1.0.7-alpha/lib` にある)`RProvider.dll` と
同じディレクトリにコピーする必要があります。
そうするとR プロバイダーがこのプラグインを自動的に検出します。

<a name="frames"></a>

データフレームをRとの間でやりとりする
-------------------------------------

### RからDeedleへ

まずはデータフレームをRからDeedleに渡す方法から見ていきましょう。
これをテストするために、`datasets` パッケージにある、
いくつかのサンプルデータセットを使用します。
Rのすべてのパッケージは `RProvider` 名前空間以下に用意されているため、
`datasets` をオープンして `R.mtcars` とすれば `mtcars` にアクセスできます
(`R`に続けてドットを入力すれば自動的に補完候補が表示されるはずです)：

*)
(*** define-output:mtcars ***)
open RProvider.datasets

// 型無しオブジェクトとして mtcars を取得します
R.mtcars.Value

(*** include-it:mtcars ***)

// mtcars を型付きのDeedleフレームとして取得します
let mtcars : Frame<string, string> = R.mtcars.GetValue()

(**
1番目のサンプルでは `Value` プロパティを参照して、
データセットをDeedleの`obj`型にボックス化されたフレームへと変換しています。
データを探索するにはこれでも素晴らしいのですが、
さらに複雑な処理を行いたい場合にはデータフレームの型を指定する必要があります。
具体的には13行目のようにすると、
行と列が`string`型でインデックスされたDeedleフレームとして`mtcars`を取得できます。

これが標準的なDeedleのデータフレームであることを確認するために、
この車両データをギア数でグループ化して、
ギアをベースとした平均「燃費(1ガロンあたりの走行マイル数)」を計算します。
データを可視化するためには
[F# Charting library](https://github.com/fsharp/FSharp.Charting)
を使用します：

*) 
(*** define-output:mpgch ***)
#load "FSharp.Charting.fsx"
open FSharp.Charting

mtcars
|> Frame.groupRowsByInt "gear"
|> Frame.getCol "mpg"
|> Stats.levelMean fst
|> Series.observations |> Chart.Column

(*** include-it:mpgch ***)

(**

### DeedleからRへ

これまではRのデータフレームをDeedleの`Frame<R, C>`に変換する方法を紹介しましたが、
次に反対方向の変換を紹介しましょう。
以下のスニペットでは、まずCSVファイルからDeedleデータフレームを読み取ります
(ファイル名は`airQuality`変数として保持されています)。
そしてデータフレームを受け取る標準R関数の引数にこのデータフレームを指定します。
*)

let air = Frame.ReadCsv(airQuality, separators=";")

(*** include-value:air ***)

(**
まず最初に、`air` フレームをRの`as_data_frame`関数に渡してみます
(この関数はデータをRにインポートするだけで、それ以外は何も行いません)。
もう少し興味深い処理として、Rの`colMeans`関数を呼び出して、
各列に対する平均を計算してみます
(そのためにはあらかじめ`base`パッケージをオープンしておく必要があります)：
*)
open RProvider.``base``

// airデータをRに渡して、Rの出力を表示します
R.as_data_frame(air)

// airデータをRに渡して、列の平均を計算します
R.colMeans(air)
// [fsi:val it : SymbolicExpression =]
// [fsi:  Ozone  Solar.R  Wind  Temp  Month   Day ]
// [fsi:    NaN      NaN  9.96 77.88   6.99  15.8]

(** 
最後の例として、値無しの処理について説明します。
Rと異なり、Deedleではデータ無し(`NA`)と非数(`NaN`)が区別されません。
たとえば以下の単純なフレームにおいて、`Floats`列のキー2と3は値無し、
一方`Names`列は2の行が値無しです：
*)
// 値無しを含んだサンプルデータを作成
let df = 
  [ "Floats" =?> series [ 1 => 10.0; 2 => nan; 4 => 15.0]
    "Names"  =?> series [ 1 => "one"; 3 => "three"; 4 => "four" ] ] 
  |> frame
(**
このデータフレームをRに渡すと、数値的な列にある値無しは`NaN`、
その他の列にあるデータ無しは`NA`になります。
以下では現在のR環境下で利用可能な変数にデータフレームを格納する関数
 `R.assign` を使用しています：
*)
R.assign("x",  df)
// [fsi:val it : SymbolicExpression = ]
// [fsi:     Floats   Names ]
// [fsi: 1       10     one ] 
// [fsi: 2      NaN    <NA> ]
// [fsi: 4       15    four ]
// [fsi: 3      NaN   three ]
(**

<a name="series"></a>

時系列データをRとの間でやりとりする
-----------------------------------

Deedleプラグインは時系列データを処理するために
[zoo package](http://cran.r-project.org/web/packages/zoo/index.html) 
(Z's ordered observations：Zの順序付き観測値)を使用しています。
このパッケージをまだインストールしていない場合、
R上で`install.packages("zoo")`コマンドを実行するか、
以下のF#コードを実行します
(F#コードを実行した場合、インストール完了後に
エディタとF# Interactiveを一度終了する必要があります)：
*)

open RProvider.utils
R.install_packages("zoo")

(**
### RからDeedleへ

まずはRから時系列データを取得する方法を紹介します。
今回もまたサンプル用に`datasets`パッケージを使用します。
たとえば`austres`データセットにはオーストラリアの人口に関する4半期毎の
時系列データが含まれています：
*)
R.austres.Value
// [fsi:val it : obj =]
// [fsi:    1971.25 -> 13067.3 ]
// [fsi:    1971.5  -> 13130.5 ]
// [fsi:    1971.75 -> 13198.4 ]
// [fsi:    ...     -> ...     ]
// [fsi:    1992.75 -> 17568.7 ]
// [fsi:    1993    -> 17627.1 ]
// [fsi:    1993.25 -> 17661.5 ]
(**
データフレームと同様、時系列データに対してもっと複雑な処理を行いたい場合には、
型アノテーションを指定して`GetValue`ジェネリックメソッドを呼び出します。
以下では、キーと値がいずれも`float`型であるシリーズを取得したいということを
F#コンパイラに伝えています：
*)
// オーストラリアの人口を含んだシリーズを取得
let austres : Series<float, float> = R.austres.GetValue()

// (約)2年間を表すTimeSpanを取得
let twoYears = TimeSpan.FromDays(2.0 * 365.0)

// 2年単位でスライドするウィンドウ毎の平均を計算
austres 
|> Series.mapKeys (fun y -> 
    DateTime(int y, 1 + int (12.0 * (y - floor y)), 1))
|> Series.windowDistInto twoYears Stats.mean
(**

現在のバージョンのDeedleプラグインでは、
単一の列を持った時系列データだけがサポートされています。
たとえばEUの株式市場データにアクセスする場合、
対象とする列を展開するRの短いインラインコードを作成する必要があります。
以下のコードでは`EuStockMarkets`からFTSE時系列データを取得しています：

*)
let ftseStr = R.parse(text="""EuStockMarkets[,"FTSE"]""")
let ftse : Series<float, float> = R.eval(ftseStr).GetValue()
(**

### DeedleからRへ

逆方向の処理も同じく簡単です。
デモとして、本日から3日間、ランダムに生成された値を含む
単純な時系列データを生成します：
*)
let rnd = Random()
let ts = 
  [ for i in 0.0 .. 100.0 -> 
      DateTime.Today.AddHours(i), rnd.NextDouble() ] 
  |> series
(**
時系列データが用意出来たので、
`R.as_zoo`あるいは`R.assign`関数を使用してRの変数に格納します。
先ほどと同じく、RプロバイダーはRが出力した値を自動的に画面に表示します：
*)
open RProvider.zoo

// 時系列データを単にRのデータに変換します
R.as_zoo(ts)
// データを変換して変数'ts'に割り当てます
R.assign("ts", ts)
// [fsi:val it : string =
// [fsi: 2013-11-07 05:00:00 2013-11-07 06:00:00 2013-11-07 07:00:00 ...]
// [fsi: 0.749946652         0.580584353         0.523962789         ...]

(**
時系列データはそれを引数にとる関数に直接渡すことが出来るため、
一般的には時系列データをRの変数に割り当てる必要はありません。
たとえば以下のスニペットでは、時系列データに対してウィンドウサイズ20で
回転平均を計算する関数を呼び出しています。
*)
// ウィンドウサイズ20で回転平均を計算
R.rollmean(ts, 20)

(**
これは単純な例です。
実際、`Series.window`関数をDeedleから呼び出せば同じ結果が得られます。
しかしこのデモからは、時系列データ(およびデータフレーム)を処理するRパッケージを
Deedleから簡単に呼び出すことが出来るということがわかります。
最後の例として、元の時系列データと回転平均の両方を(別の列として)
含むデータフレームを作成し、結果をチャートとして表示させます：
*)

(*** define-output:means ***)
// 'rollmean'で平均を計算した後、'GetValue'で結果を
// Deedleの時系列データとして取得します
let tf = 
  [ "Input" => ts 
    "Means5" => R.rollmean(ts, 5).GetValue<Series<_, float>>()
    "Means20" => R.rollmean(ts, 20).GetValue<Series<_, float>>() ]
  |> frame

// 元の入力データと2つの回転平均をチャートにします
Chart.Combine
  [ Chart.Line(Series.observations tf?Input)
    Chart.Line(Series.observations tf?Means5)
    Chart.Line(Series.observations tf?Means20) ]

(**
生成された乱数次第ですが、結果はおよそ以下のようなものになります：
*)

(*** include-it:means ***)
