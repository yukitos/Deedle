﻿(*** hide ***)
#I "../../../bin"
#load "Deedle.fsx"
#I "../../../packages/MathNet.Numerics.3.0.0-beta01/lib/net40"
#load "../../../packages/FSharp.Charting.0.90.6/FSharp.Charting.fsx"
open System
open FSharp.Data
open Deedle
open FSharp.Charting

let root = __SOURCE_DIRECTORY__ + "/data/"

(**
シリーズおよび時系列データを F# で扱う
======================================

このセクションでは時系列データ、あるいはさらに汎用的に、
任意の順序付きシリーズデータを扱うための
F# データフレームライブラリの機能について説明します。
主には `Series` 型の操作について説明しますが、
ほとんどは複数のシリーズを含むデータフレームを表す `Frame` 型でも
利用出来ます。
さらに、データフレームにはアライメントやシリーズの連結を行うための
すばらしい機能も用意されています。

このページをGitHubから
[F#スクリプトファイル](https://github.com/BlueMountainCapital/Deedle/blob/master/docs/content/series.fsx)
としてダウンロードして、インタラクティブに実行することもできます。

入力データの生成
----------------

このチュートリアル用にいくつか入力データを用意します。
簡単のために、ここでは幾何ブラウン運動を使用して
ランダムな価格を生成する、以下のような関数を使用します。
このコードは
[Try F#のファイナンス用チュートリアル](http://www.tryfsharp.org/Learn/financial-computing#simulating-and-analyzing)
から抜粋したものです。

*)

// 確率分布の計算のために Math.NET を使用します
#r "MathNet.Numerics.dll"
open MathNet.Numerics.Distributions

/// 幾何ブラウン運動を使用して価格を生成します
///  - 'seed' には乱数生成器のシードを指定します
///  - 'drift' と 'volatility' には価格変動の特性を設定します
///  - 'initial' と 'start' には初期の価格と日付を指定します
///  - 'span' にはそれぞれの測定の間隔を指定します
///  - 'count' には生成する値の個数を指定します
let randomPrice seed drift volatility initial start span count = 
  (*[omit:(実装については省略)]*) 
  let dist = Normal(0.0, 1.0, RandomSource=Random(seed))  
  let dt = (span:TimeSpan).TotalDays / 250.0
  let driftExp = (drift - 0.5 * pown volatility 2) * dt
  let randExp = volatility * (sqrt dt)
  ((start:DateTimeOffset), initial) |> Seq.unfold (fun (dt, price) ->
    let price = price * exp (driftExp + randExp * dist.Sample()) 
    Some((dt, price), (dt + span, price))) |> Seq.take count(*[/omit]*)

// 現在のタイムゾーンにおける本日午前 12:00 のデータ
let today = DateTimeOffset(DateTime.Today)
let stock1 = randomPrice 1 0.1 3.0 20.0 today 
let stock2 = randomPrice 2 0.2 1.5 22.0 today
(**
この関数の実装についてはこのページの目的からすると重要では無いので省略していますが、
[完全なコードを含んだスクリプトファイル](https://github.com/BlueMountainCapital/Deedle/blob/master/docs/content/series.fsx)
には実際のコードがあります。

この関数を定義した後、(本日の真夜中を表す) `today` という日付データと、
基本的な性質を設定して `randomPrice` を呼び出す2つのヘルパ関数を定義しています。

そのため `TimeSpan` と、必要になる価格の個数を指定して
`stock1` と `stock2` を呼び出すだけでランダムな価格を取得することができます：
*)
(*** define-output: stocks ***)
Chart.Combine
  [ stock1 (TimeSpan(0, 1, 0)) 1000 |> Chart.FastLine
    stock2 (TimeSpan(0, 1, 0)) 1000 |> Chart.FastLine ]
(**
このスニペットでは1分間隔で1000個の価格データを生成した後、
[F# Chartingライブラリ](https://github.com/fsharp/FSharp.Charting)
を使用してそれらを表にプロットしています。
このコードを実行してチャートを確認するとおよそ以下のようなものになっているはずです：
*)

(*** include-it: stocks ***)

(**
<a name="alignment"></a>
データアライメントとジップ
--------------------------

データフレームライブラリの主要な機能の1つとして、
複数キーを元にした時系列データの **自動アライメント (automatic alignment)**
があります。
複数の時系列データがキーになっているデータがあるとすると
(今回は `DateTimeOffset` ですが、任意の型が使用できます)、
複数のシリーズを連結して特定の日付キーでそれらをアラインすることができます。

この機能を紹介するために、60分、30分、65分間隔のランダムな価格を生成します：
*)

let s1 = series <| stock1 (TimeSpan(1, 0, 0)) 6
// [fsi:val s1 : Series<DateTimeOffset,float> =]
// [fsi:  series [ 12:00:00 AM => 20.76; 1:00:00 AM => 21.11; 2:00:00 AM => 22.51 ]
// [fsi:            3:00:00 AM => 23.88; 4:00:00 AM => 23.23; 5:00:00 AM => 22.68 ] ]

let s2 = series <| stock2 (TimeSpan(0, 30, 0)) 12
// [fsi:val s2 : Series<DateTimeOffset,float> =]
// [fsi:  series [ 12:00:00 AM => 21.61; 12:30:00 AM => 21.64; 1:00:00 AM => 21.86 ]
// [fsi:            1:30:00 AM => 22.22;  2:00:00 AM => 22.35; 2:30:00 AM => 22.76 ]
// [fsi:            3:00:00 AM => 22.68;  3:30:00 AM => 22.64; 4:00:00 AM => 22.90 ]
// [fsi:            4:30:00 AM => 23.40;  5:00:00 AM => 23.33; 5:30:00 AM => 23.43] ]

let s3 = series <| stock1 (TimeSpan(1, 5, 0)) 6
// [fsi:val s3 : Series<DateTimeOffset,float> =]
// [fsi:  series [ 12:00:00 AM => 21.37; 1:05:00 AM => 22.73; 2:10:00 AM => 22.08 ]
// [fsi:            3:15:00 AM => 23.92; 4:20:00 AM => 22.72; 5:25:00 AM => 22.79 ]

(**
### 時系列の結合

まずは `Series<K, V>` に対して利用出来る操作を紹介します。
シリーズには2つのシリーズをペアにして1つのシリーズとする `Zip`
操作が用意されています。
これは(後ほど説明する)データフレームではそれほど便利ではないのですが、
値無しを含まない1つまたは2つの列を処理する場合には便利なものです：

*)
// 左側のシリーズのキーと右側のシリーズの値をマッチさせます
// (それにより値無しを含まないシリーズが作成されます)
s1.Zip(s2, JoinKind.Left)
// [fsi:val it : Series<DateTimeOffset,float opt * float opt>]
// [fsi:  12:00:00 AM -> (21.32, 21.61) ]
// [fsi:   1:00:00 AM -> (22.62, 21.86) ]
// [fsi:   2:00:00 AM -> (22.00, 22.35)  ]
// [fsi:  (...)]

// 右側のシリーズのキーと左側のシリーズの値をマッチさせます
// (右側のほうが精度が高いため、左側の値の半分が値無しになります)
s1.Zip(s2, JoinKind.Right)
// [fsi:val it : Series<DateTimeOffset,float opt * float opt>]
// [fsi:  12:00:00 AM -> (21.32,     21.61) ]
// [fsi:  12:30:00 AM -> (<missing>, 21.64)  ]      
// [fsi:   1:00:00 AM -> (22.62,     21.86) ]
// [fsi:  (...)]

// 左側のシリーズのキーを使用して、右側のシリーズから最も近い以前の
// (値として小さな)値を見つけるようにします
s1.Zip(s2, JoinKind.Left, Lookup.ExactOrSmaller)
// [fsi:val it : Series<DateTimeOffset,float opt * float opt>]
// [fsi:  12:00:00 AM -04:00 -> (21.32, 21.61) ]
// [fsi:   1:00:00 AM -04:00 -> (22.62, 21.86) ]
// [fsi:   2:00:00 AM -04:00 -> (22.00, 22.35)  ]
// [fsi:  (...)]

(**
シリーズに対して `Zip` を呼び出した結果はやや複雑なものになります。
結果はシリーズのタプルになるのですが、各タプルの要素は値無しになることがあります。
このことを表現するために、ライブラリでは `T opt` という型を使用しています
(`OptionalValue<T>` の型エイリアスです)。
この値はデータフレームを使用して複数の列を扱う場合には不要なものです。

### データフレームの連結

データをデータフレームに格納する際、タプルを使用して組み合わされた値を
表現する必要はありません。
そのかわり、単に複数の列を持ったデータフレームを使用できます。
具体的な動作を紹介するために、まずは以前のセクションで用意した3つのシリーズを含んだ
3つのデータフレームを用意します：
*)

// 1時間毎の値を含みます
let f1 = Frame.ofColumns ["S1" => s1]
// 30分毎の値を含みます
let f2 = Frame.ofColumns ["S2" => s2]
// 65分毎の値を含みます
let f3 = Frame.ofColumns ["S3" => s3]

(**
`Series<K, V>` と同じく、 `Frame<R, C>` には
(順序づけられていないデータに対する)ジョイン、
あるいは(順序付きデータに対する)アラインを行う `Join` メソッドが用意されています。
同じ機能が `Frame.join` や `Frame.joinAlign` 関数として定義されていますが、
今回の場合にはメンバーメソッドの文法を使用したほうが簡単です：
*)

// 両方のフレームのキーを結合して、対応する値でアラインします
f1.Join(f2, JoinKind.Outer)
// [fsi:val it : Frame<DateTimeOffset,string> =]
// [fsi:                 S1        S2               ]
// [fsi:  12:00:00 AM -> 21.32     21.61 ]
// [fsi:  12:30:00 AM -> <missing> 21.64 ]
// [fsi:   1:00:00 AM -> 22.62     21.86 ]
// [fsi:  (...)]

// 両方のフレームが値を持つキーだけを取得します
// ('f3' は5分ずつずれているため1行しか取得できません)
f2.Join(f3, JoinKind.Inner)
// [fsi:val it : Frame<DateTimeOffset,string> =]
// [fsi:                 S2      S3               ]
// [fsi:  12:00:00 AM -> 21.61   21.37 ]

// 左のフレームのキーを取得して、右のフレームから対応する、または
// 最も近い小さな日付の値を取得します
// (12:00 から 1:05にかけて 21.37 ドルが繰り返されます)
f2.Join(f3, JoinKind.Left, Lookup.ExactOrSmaller)
// [fsi:val it : Frame<DateTimeOffset,string> =]
// [fsi:                 S2      S3               ]
// [fsi:  12:00:00 AM -> 21.61   21.37 ]
// [fsi:  12:30:00 AM -> 21.64   21.37 ]
// [fsi:   1:00:00 AM -> 21.86   21.37 ]
// [fsi:   1:30:00 AM -> 22.22   22.73 ]
// [fsi:  (...)]

// 厳密マッチングでレフトジョインを行うと
// ほとんどが値無しになります
f2.Join(f3, JoinKind.Left, Lookup.Exact)
// [fsi:val it : Frame<DateTimeOffset,string> =]
// [fsi:                 S2      S3               ]
// [fsi:  12:00:00 AM -> 21.61   21.37]
// [fsi:  12:30:00 AM -> 21.64   <missing>        ]
// [fsi:   1:00:00 AM -> 21.86   <missing>        ]
// [fsi:  (...)]

// 関数シンタックスで2行目と同じことを行います
Frame.join JoinKind.Outer f1 f2

// 関数シンタックスで20行目と同じことを行います
Frame.joinAlign JoinKind.Left Lookup.ExactOrSmaller f1 f2

(**
それぞれの観測値間で異なるオフセットを持った複数のデータシリーズを扱う場合、
自動アライメントの機能はきわめて便利なものです。
キーのセット(日付)を選択するだけで、簡単にキーと一致する
別のデータを整列させることができます。
`Join` を明示的に使用しない方法としては、
(`Frame.ofRowKeys` を使用して)対象とするキーを持った新しいフレームを作成し、
`AddSeries` メンバー(または `df?New <- s` のシンタックス)を使用して
シリーズを追加することもできます。
この場合には新しいシリーズに対して、現在の行キーに一致するよう自動的に
レフトジョインが行われます。

データをアラインする際、値無しを含んだデータフレームを作成したい、
あるいは作成したくないことがあります。
観測値が厳密な時刻を持たない場合、ミスマッチを防ぐには
`Lookup.ExactOrSmaller` または `Lookup.ExactOrGreater` を使用するとよいでしょう。

観測値がたとえばたまたま倍の頻度(1つは1時間毎、もう1つは30分毎)
になっているのであれば、(デフォルト値である) `Lookup.Exact` を使用すると
値無しを含むデータフレームを作成でき、
([こちらで説明しているように](frame.html#missing))
明示的に値無しを処理することができます。

<a name="windowing"></a>
ウィンドウ化、チャンク化、ペア化
--------------------------------

ウィンドウ化とチャンク化は順序付きシリーズの値をグループとして集計する操作です。
これらの操作は順序を考慮しない [グループ化](tutorial.html#grouping) とは異なり、
連続する要素を対象とするものです。

### ウィンドウのスライディング

スライディングウィンドウは特定の大きさのウィンドウ(あるいは特定の条件)を作成します。
ウィンドウは入力シリーズ全体を「スライド」していき、シリーズの一部分を返します。
一般的には複数のウィンドウ内に単一の要素しか現れないという点が重要です。
*)

// 6個の観測値を含む入力用シリーズを作成します
let lf = series <| stock1 (TimeSpan(0, 1, 0)) 6

// 各ウィンドウを表すシリーズのシリーズを作成します
lf |> Series.window 4
// 'Stats.mean'を使用して各ウィンドウを集計します
lf |> Series.windowInto 4 Stats.mean
// 各ウィンドウの最初の値を取得します
lf |> Series.windowInto 4 Series.firstValue

(**
上のコードで使用している関数は左から右に移動する、
サイズが4のウィンドウを作成しています。
つまり入力値が `[1,2,3,4,5,6]` とすると、
`[1,2,3,4]` `[2,3,4,5]` `[3,4,5,6]`
という3つのウィンドウが生成されることになります。
デフォルトでは `Series.window` 関数はウィンドウの最後の要素のキーを
ウィンドウ全体のキーとして選択します
(この挙動を変更する方法についてはもう少し後で説明します)：

*)
// スライディングウィンドウの平均を計算します
let lfm1 = lf |> Series.windowInto 4 Stats.mean
// アラインされた結果を表示するためのデータフレームを作成します
Frame.ofColumns [ "Orig" => lf; "Means" => lfm1 ]
// [fsi:val it : Frame<DateTimeOffset,string> =]
// [fsi:                 Means      Orig ]
// [fsi:  12:00:00 AM -> <missing>  20.16]
// [fsi:  12:01:00 AM -> <missing>  20.32]
// [fsi:  12:02:00 AM -> <missing>  20.25]
// [fsi:  12:03:00 AM -> 20.30      20.45]
// [fsi:  12:04:00 AM -> 20.34      20.32]
// [fsi:  12:05:00 AM -> 20.34      20.33]

(**
`<missing>` の値が出来てしまうのを避けたい場合にはどうしたらよいでしょうか？
1つのアプローチとしては開始時刻の先頭または末尾で小さなサイズのウィンドウを
生成するようにします。
この場合、 `[1]` `[1,2]` `[1,2,3]` という**不完全な**ウィンドウに続けて、
上の結果にある3つの**完全な**ウィンドウが生成されることになります：
*)
let lfm2 = 
  // 先頭では不完全なサイズのスライディングウィンドウを作成します
  lf |> Series.windowSizeInto (4, Boundary.AtBeginning) (fun ds ->
    Stats.mean ds.Data)

Frame.ofColumns [ "Orig" => lf; "Means" => lfm2 ]
// [fsi:val it : Frame<DateTimeOffset,string> =]
// [fsi:                 Means  Orig ]
// [fsi:  12:00:00 AM -> 20.16  20.16]
// [fsi:  12:01:00 AM -> 20.24  20.32]
// [fsi:  12:02:00 AM -> 20.24  20.25]
// [fsi:  12:03:00 AM -> 20.30  20.45]
// [fsi:  12:04:00 AM -> 20.34  20.32]
// [fsi:  12:05:00 AM -> 20.34  20.33]

(**
見ての通り、1行目では1つしかデータが無いシリーズの
`Mean` (平均)を計算しているわけなので、
いずれも同じ値になっています。

(今回の例のように) `Boundary.AtBeginning` や
(1つ前の例で使用している、デフォルト値) `Boundary.Skip` を指定すると、
この関数はウィンドウの最後のキーを集計値のキーとして
使用するようになります。
また、 `Boundary.AtEnding` を指定すると最初のキーが使用されるため、
オリジナルの値をいい感じにアラインすることができます。
独自のキーセレクター指定したい場合には、さらに汎用的な関数
`Series.aggregate` を使用します。

この例では、集計を行うコードが `Stats.mean` のような単なる関数ではなく、
`DataSegment<T>` 型の引数 `ds` をとるラムダ式になっています。
この型にはウィンドウが完全かどうかを示す情報が含まれています。
たとえば以下のようにできます：
*)

// 文字を含んだ単純なシリーズ
let st = Series.ofValues [ 'a' .. 'e' ]
st |> Series.windowSizeInto (3, Boundary.AtEnding) (function
  | DataSegment.Complete(ser) -> 
      // 完全なウィンドウに対しては大文字の文字列を返す
      String(ser |> Series.values |> Array.ofSeq).ToUpper()
  | DataSegment.Incomplete(ser) -> 
      // 不完全なウィンドウに対しては小文字かつ字詰めした文字列を返す
      String(ser |> Series.values |> Array.ofSeq).PadRight(3, '-') )  
// [fsi:val it : Series<int,string> =]
// [fsi:  0 -> ABC ]
// [fsi:  1 -> BCD ]
// [fsi:  2 -> CDE ]
// [fsi:  3 -> de- ]
// [fsi:  4 -> e-- ]

(**
### ウィンドウのサイズ条件

先の例では固定サイズのウィンドウを生成しました。
しかしウィンドウの終点を指定する方法としてはさらに2つのオプションがあります。

 - 1つ目のオプションは始点キーと終点キーの最大**距離**を指定する方法です
 - 2つ目のオプションは始点キーと終点キーで呼び出される関数を指定する方法です。
   この関数は終点に至るとfalseを返します。

これらはそれぞれ `Series.windowDist` と `Series. windowWhile` として
実装されています
(また、各ウィンドウを集計するために呼び出される関数を指定することができる、
`Into` サフィックスのついたバージョンもあります)：
*)
// 30日間の価格を1時間単位にまとめます
let hourly = series <| stock1 (TimeSpan(1, 0, 0)) (30*24)

// 1日単位のウィンドウを作成します(ソースがイレギュラーなデータを含む場合、
// ウィンドウのサイズはまちまちになります)
hourly |> Series.windowDist (TimeSpan(24, 0, 0))

// 同じ日付のデータが同じウィンドウに入るようなウィンドウを生成します
// (各ウィンドウは0時に始まり、同日の最終時刻データまでが含まれます)
hourly |> Series.windowWhile (fun d1 d2 -> d1.Date = d2.Date)

(**
### シリーズのチャンク化

チャンク化(chunking)はウィンドウ化に似ていますが、
(重複する)スライディングウィンドウとは異なり、
重複部分のないチャンク(訳注：データの小さなかたまり)が生成されます。
チャンクのサイズはスライディングウィンドウの場合と同じく、
3つの方法で指定できます(固定長、キー間の距離および特定条件)：
*)

// 10分間、1秒単位の観測値を生成します
let hf = series <| stock1 (TimeSpan(0, 0, 1)) 600

// 10秒単位のチャンクを作成して、(おそらくは)末尾では
// 10秒未満のチャンクとなるようにします。
hf |> Series.chunkSize (10, Boundary.AtEnding) 

// 10秒間隔でチャンクを作成して、
// 各チャンクにある最初の観測値(ダウンサンプル)を取得します
hf |> Series.chunkDistInto (TimeSpan(0, 0, 10)) Series.firstValue

// hh:mm(時分)の値が同じもの同士でチャンクを作成します
// (すべての秒データがいずれかのチャンクに含まれることになります)
hf |> Series.chunkWhile (fun k1 k2 -> 
  (k1.Hour, k1.Minute) = (k2.Hour, k2.Minute))

(**
上の例では非常によく似た方法で様々なチャンク化関数を呼び出しています。
これは主に、ランダムに生成された入力データが非常に均一だというのが理由です。
しかし均一ではないキーを持った入力に対しては、
上のそれぞれの関数は異なった動作になります。

`chunkSize` を使用する場合、チャンクのサイズは同じになりますが、
時間の間隔が異なるシリーズに対応する場合があります。
`chunkDist` ではそれぞれのチャンクにおける最大の時刻が存在することが保証されますが、
いつチャンクが開始されるのかは保証されません。
これは `chunkWhile` を使用した場合も同様です。

最後に、これまで紹介した集計関数はいずれも `Series.aggregate`
の特化版であることを明記しておきます。
この関数には集計の種類を判別共用体で指定できます。
([APIリファレンスを参照してください](../reference/fsharp-dataframe-aggregation-1.html))
しかし実際のところはここで紹介したヘルパー関数を使用した方が簡単です。
一部のまれなケースにおいては、 `Series.aggregate` にいくつかオプションを指定して
呼び出す必要があるかもしれません。

### ペア化

ウィンドウ化の特別版として、入力されたシリーズから現在および直前の値を含んだ
一連のペアを生成することができます
(別の言い方をすると、各ペアのキーがペアの後者であるようなウィンドウということです)。
たとえば以下のようにします：
*)

// 前述の 'hf' から一連のペアを生成します
hf |> Series.pairwise 

// 現在の値と直前の値の差を計算します
hf |> Series.pairwiseWith (fun k (v1, v2) -> v2 - v1)

(** 
`pairwise` 演算では、入力されたシリーズの最初の値をキーとする値を
含まないようなシリーズが常に返されます。
さらに複雑な動作をさせる必要がある場合には、
`pairwise` を `window` に置き換えることになります。
たとえば最初の要素としてシリーズの最初の値を含み、
以降にはペアの差分を含むようなシリーズを取得したいとします。
このシリーズには、最初の値から行を加算していくと
それぞれの時点における価格が計算できるという素敵な性質があります：
*)
// 一番最初は不完全なセグメントとなるスライディングウィンドウ
hf |> Series.windowSizeInto (2, Boundary.AtBeginning) (function
  // 1番目のセグメントに対しては1つめの値を返す
  | DataSegment.Incomplete s -> s.GetAt(0)
  // その他のセグメントに対しては差分を計算する
  | DataSegment.Complete s -> s.GetAt(1) - s.GetAt(0))

(**

<a name="sampling"></a>
時系列データのサンプリングおよび再サンプリング
----------------------------------------------

高頻度の価格データを持った時系列データに対して、
サンプリングあるいは再サンプリングを行うと頻度の低い値を含んだ
時系列データを取得することができます。
このライブラリでは以下の用語を使用します：

 - **ルックアップ(Lookup)** とは、
   特定のキーに対する値を検索することを表します。
   なおキーが利用できない場合、指定した値に最も近く、それよりも小さいか大きい値を
   見つけることもできます。

 - **再サンプリング(Resampling)** とは、
   特定のキーコレクション(たとえば明示的に指定した時刻)、
   あるいはキー同士の関係性(たとえば日付が同じ時刻)
   を元にして値を集計することを表します。

 - **ユニフォーム再サンプリング(Uniform resampling)** は
   再サンプリングと似ていますが、一意なキーのシーケンス(たとえば日付)を生成する
   関数によってキーを指定します。
   またこの関数では、キーに対応する値が入力シーケンス中に見つからなかった場合に
   値を埋めさせることもできます。

なおこのライブラリにはキーの型が `DateTime` あるいは `DateTimeOffset` の
シリーズデータに特化したヘルパー関数がいくつか用意されています。

### ルックアップ

シリーズ `hf` に対して、 `hf.Get(key)` または `hf |> SEries.get key` とすると
特定のキーに対する1つの値が取得できます。
しかし複数のキーに対する複数の値を同時に取得することも可能です。
そのためのインスタンスメンバーが `hf.GetItems(..)` です。
また、 `Get` および `GetItems` にはオプションとして
キーに厳密に一致する値が見つからなかった場合の挙動を指定することもできます。

関数シンタックスを使用する場合、厳密なキールックアップを
行う場合には `Series.getAll`、より柔軟なルックアップを行いたい場合には
`Series.lookupAll` を使用します：
*)
// 13.7秒間隔で24時間以内のデータを生成します
let mf = series <| stock1 (TimeSpan.FromSeconds(13.7)) 6300
// 1分間隔で24時間以内のキーを生成します
let keys = [ for m in 0.0 .. 24.0*60.0-1.0 -> today.AddMinutes(m) ]

// 特定のキーに一致する値、または最も近く大きい値を検索します
mf |> Series.lookupAll keys Lookup.ExactOrGreater
// [fsi:val it : Series<DateTimeOffset,float> =]
// [fsi:  12:00:00 AM -> 20.07 ]
// [fsi:  12:01:00 AM -> 19.98 ]
// [fsi:  ...         -> ...   ]
// [fsi:  11:58:00 PM -> 19.03 ]
// [fsi:  11:59:00 PM -> <missing>        ]

// 最も近く小さいキーに対する値を検索します
// (この場合には午後11:59:00の値が返されます)
mf |> Series.lookupAll keys Lookup.ExactOrSmaller

// キーに厳密に一致する値を検索します
// (1番目のキーに対してのみ機能します)
mf |> Series.lookupAll keys Lookup.Exact

(**
ルックアップ操作ではキーそれぞれに対して1つの値しか返されません。
そのため巨大な(あるいは高頻度の)データから
簡単にサンプリングする場合に便利なものです。
複数の値を元にして新しい値を計算したい場合には
再サンプリングする必要があります。

### 再サンプリング

シリーズでは2種類の再サンプリングがサポートされています。
1つめは明示的にキーを指定しなければならないルックアップとよく似ています。
違いとして、再サンプリングでは直近のキーだけではなく、
すべてのより小さなキー、あるいはより大きなキーが見つかります。
たとえば以下のようになります：
*)

// 各キーに対して、大きいキーまでにある値を取得します
// (午後11:59:00のチャンクは空になります)
mf |> Series.resample keys Direction.Forward

// 各キーに対して、小さいキーまでにある値を取得します
// (最初のチャンクにはシリーズ1つだけが含まれます)
mf |> Series.resample keys Direction.Backward

// チャンク内にある一連の値の平均を集計します
mf |> Series.resampleInto keys Direction.Backward 
  (fun k s -> Stats.mean s)

// 再サンプリングはメンバ構文でも実行できます
mf.Resample(keys, Direction.Forward)
(**

2つめの方法として、シリーズ内にある既存のキーに対する射影を元にして
再サンプリングすることもできます。
この操作では、射影によって同じキーが返されるようなチャンクを収集します。
この動作は `Series.groupBy` と非常によく似ていますが、
再サンプリングでは射影によってキーの順序が維持されることが想定されます。
そのため、連続するキーだけが集計されます。

一般的なシナリオとしては、時刻情報(今回の場合は`DateTimeOffset`)を含んだ
時系列データがあり、日単位の情報を取得したい場合が想定できます
(ここでは日付を表すために、時刻データが空の `DateTime` を使用します)：
*)

// 1.7時間毎、2.5ヶ月分のデータを生成します
let ds = series <| stock1 (TimeSpan.FromHours(1.7)) 1000

// ('DateTime'型の)日単位でサンプリング
ds |> Series.resampleEquiv (fun d -> d.Date)

// ('DateTime'型の)日単位でサンプリング
ds.ResampleEquivalence(fun d -> d.Date)
(**

同じ処理は `Series.chunkWhile` を使用して簡単に実装できますが、
サンプリングという文脈で使用されることが多いことも有り、
ライブラリに標準実装してあります。
さらに、ユニフォーム再サンプリングと密接な関係があることを後ほど説明します。

なお結果として返されるシリーズは元とは異なる型のキーを持つことに注意してください。
元データでは(時刻を含む日付を表す) `DateTimeOffset` 型のキーでしたが、
結果のキーは射影によって返される型になります
(今回の場合には日付だけが有効な `DateTime` 型です)。

### ユニフォーム再サンプリング

先のセクションでは、「解像度の低い(lower resolution)」キーを持った時系列データへと
サンプリングしたい場合には `resampleEquiv` が使用できることを説明しました
(例として日時の観測値を日付単位にサンプリングしました)。
しかし先のセクションで説明した関数は、
入力シーケンス中にキーが存在する場合にのみ値を生成します。
つまり観測値が丸一日存在しない場合、その日のデータは結果に含まれないことになります。

入力シーケンスによって指定されている範囲内にある各キーに対して
値を割り当てるようなサンプリングを行いたい場合には
**ユニフォーム再サンプリング** を行うことになります。

ユニフォーム再サンプリングとは、最小および最大の入力キーに対して
(たとえば観測値の最初および最後の日付を取得するような)射影を適用して、
射影空間(たとえばすべての日付)内ですべてのキーを生成し、
生成したキーの中から最善の値をピックアップするものです。
*)

// 非ユニフォームな、分散キーを持った入力データを作成します
// (10/3には1つ、10/4には3つ、10/6には2つのデータが含まれます)
let days =
  [ "10/3/2013 12:00:00"; "10/4/2013 15:00:00" 
    "10/4/2013 18:00:00"; "10/4/2013 19:00:00"
    "10/6/2013 15:00:00"; "10/6/2013 21:00:00" ]
let nu = 
  stock1 (TimeSpan(24,0,0)) 10 |> series
  |> Series.indexWith days |> Series.mapKeys DateTimeOffset.Parse

// 日付を基準にして、ユニフォーム再サンプリングを行います。
// 値無しのチャンクに対しては最も近く小さい観測値で埋めます。
let sampled =
  nu |> Series.resampleUniform Lookup.ExactOrSmaller 
    (fun dt -> dt.Date) (fun dt -> dt.AddDays(1.0))

// C#フレンドリーなメンバーシンタックスを使用して同じことを行います
// (Lookup.ExactOrSmaller はデフォルト値です)
nu.ResampleUniform((fun dt -> dt.Date), (fun dt -> dt.AddDays(1.0)))

// (結果を見やすくするために)
// 日毎に複数列を持つようなフレームへと変換します
sampled 
|> Series.mapValues Series.indexOrdinally
|> Frame.ofRows
// [fsi:val it : Frame<DateTime,int> =]
// [fsi:             0      1          2                ]
// [fsi:10/3/2013 -> 21.45  <missing>  <missing>        ]
// [fsi:10/4/2013 -> 21.63  19.83      17.51            ]
// [fsi:10/5/2013 -> 17.51  <missing>  <missing>        ]
// [fsi:10/6/2013 -> 18.80  20.93      <missing>        ]

(**
ユニフォーム再サンプリングを行うには、元のキーを(再サンプリング後の)キーへと
どのようにして射影するのか(ここでは`Date`を返しています)、
次のキーをどのように計算するのか(翌日に設定しています)、
そして値無しをどのように補填するのかを指定する必要があります。

再サンプリングの実行後、結果を見やすくするために
データをデータフレームへと変換しています。
それぞれのチャンクには実際の観測時刻がキーとして設定されているため、
これらのキーを(`Series.indexOrdinal`を使用して)単に整数へと置き換えています。
その結果、各行には日毎の観測値が整列された状態で並びます。

重要なのは各日付に対応する観測値があるということです。
つまり2013年10月5日には対応する観測値が入力中には全くないにもかかわらず、
値を持っています。
再サンプリング関数に `Lookup.ExactOrSmaller` を指定して呼び出しているため、
前日の最後の観測値である 17.51 が値として採用されています。
(`Lookup.ExactOrGreater` を指定した場合は 18.80、
`Lookup.Exact` の場合は空のシリーズになります)

### 時系列データのサンプリング

サンプリング操作として最も一般的に行われるものは、
おそらく `TimeSpan` を指定して時系列データをサンプリングすることでしょう。
この操作は既に紹介した関数をいくつか使用することで簡単に実現できますが、
ライブラリにはまさにこの用途に適したヘルパー関数が用意されています：

*)
// 1.7時間毎、1000個の観測値を生成します
let pr = series <| stock1 (TimeSpan.FromHours(1.7)) 1000

// 2時間毎にサンプリングします。
// 'Backward' は以前のすべての値をチャンクに含めることを示しています。
pr |> Series.sampleTime (TimeSpan(2, 0, 0)) Direction.Backward

// 同じ処理をメンバーシンタックスで行います。
// 'Backward' はデフォルト値です。
pr.Sample(TimeSpan(2, 0, 0))

// 2時間毎にサンプリングしつつ、直近の値を取得します
pr |> Series.sampleTimeInto
  (TimeSpan(2, 0, 0)) Direction.Backward Series.lastValue

(**
<a name="stats"></a>
計算および統計
--------------

このチュートリアルの最後のセクションとして、
時系列データに対するいくつかの計算処理について説明します。
ここで紹介する関数の多くは順序づけられていないデータフレームやシリーズも
対象とすることができます。

### シフトおよび差分

まず最初に、シリーズ内で後に続く値と比較しなければならない場合に
必要になる関数を紹介します。
この処理は既に `Series.pairwise` で行えることを紹介しました。
たいていの場合、シリーズ全体を処理するような操作を行うことによって
同じ処理が実現できます。

以下の2つの便利な関数があります：

 - `Series.diff` は現在の値とn個前の要素との差分を計算します
 - `Series.shift` はシリーズの値を特定のオフセット分だけシフトします

これらの関数の機能については以下のスニペットを参照してください：
*)
// 1.7時間毎でサンプリングされたデータを生成します
let sample = series <| stock1 (TimeSpan.FromHours(1.7)) 6

// new[i] = s[i] - s[i-1] となるよう計算します
let diff1 = sample |> Series.diff 1
// 逆順で差分を計算します
let diffM1 = sample |> Series.diff -1

// シリーズの値を1ずつシフトします
let shift1 = sample |> Series.shift 1

// 結果を確認するためにすべての結果を1つのフレームとしてアラインします
let df = 
  [ "Shift +1" => shift1 
    "Diff +1" => diff1 
    "Diff" => sample - shift1 
    "Orig" => sample ] |> Frame.ofColumns 
// [fsi:val it : Frame<DateTimeOffset,string> =]
// [fsi:                 Diff       Diff +1    Orig   Shift +1  ]
// [fsi:  12:00:00 AM -> <missing>  <missing>  21.73  <missing> ]
// [fsi:   1:42:00 AM ->  1.73       1.73      23.47  21.73     ]
// [fsi:   3:24:00 AM -> -0.83      -0.83      22.63  23.47     ]
// [fsi:   5:06:00 AM ->  2.37       2.37      25.01  22.63     ]
// [fsi:   6:48:00 AM -> -1.57      -1.57      23.43  25.01     ]
// [fsi:   8:30:00 AM ->  0.09       0.09      23.52  23.43     ]

(**
上のスニペットではまず `Series.diff` 関数を使用して差分を計算しています。
次に `Series.shift` の呼び方をデモした後、2つのシリーズに対して2項演算を
行っています(`sample - shift`)。
詳細については以降のセクションで説明します。
また、ここでは(`sample |> Series.diff 1` のような)関数表記を使用していますが、
いずれの操作もメンバーシンタックスで呼び出すこともできます。
たいていの場合にはそのほうがコード的に短くなるでしょう。
このことについても次のセクションで説明します。

### 演算子と関数

時系列データでは、`log` や `abs` のようなF#の標準関数が多数サポートされています。
また標準的な数値演算子を使用して、シリーズのすべての要素に対して
演算を行うといったこともできます。

シリーズにはインデックスがあるため、
2つのシリーズに対して2項演算を行うことができます。
この場合、シリーズを自動的にアラインして、
対応する要素間で処理を実行することになります。

*)

// 直前の値と現在の値との差を計算します
sample - sample.Shift(1)

// 先の差分に対する自然対数を計算します
log (sample - sample.Shift(1))

// 差分の平方を計算します
sample.Diff(1) ** 2.0

// 現在の値と前後2つの値の平均値を計算します
(sample.Shift(-1) + sample + sample.Shift(2)) / 3.0

// 差分の絶対値を計算します
abs (sample - sample.Shift(1))

// 平均との差分の絶対値を計算します
abs (sample - (Stats.mean sample))

(**
時系列ライブラリにはこういった方法で呼び出すことのできる関数が多数用意されています。
その他にも、(`sin`や`cos`といった)三角関数や、
(`round` `floor` `ceil`といった)ラウンド関数、
(`exp` `log` `log10`といった)対数関数などがあります。
一般的に、標準的な型に使用できるF#の組み込みの数値演算関数に対しては
時系列ライブラリでも同じ機能がサポートされています。

しかしシリーズの全要素に適用可能な計算を行うような、
独自の関数を作成するにはどうしたらよいのでしょう？
では説明していきます：
*)

// [-1.0, +1.0] の区間に値を切り詰めます
let adjust v = min 1.0 (max -1.0 v)

// すべての要素を調整します
adjust $ sample.Diff(1)

// $ 演算子は以下の省略形です
sample.Diff(1) |> Series.mapValues adjust

(**
一般的に、シリーズ内のすべての値に対して独自の関数を適用する場合の最善の方法は、
(`Series.join` または `Series.joinAlign` のいずれかを使用して)
タプルを含む1つのシリーズとしてアラインした後、
`Series.mapValues` を適用することです。
ライブラリには最後の手順を省略できるように `$` 演算子が定義されています。
つまり、 `f $ s` とするとシリーズ `s` のすべての値に関数 `f` を適用できます。

### データフレームの操作

最後に、これまでに紹介した操作の多くがデータフレームに対しても
通用することを説明しましょう。
これは(たとえば複数の株価データやローソク足データのように)
同じような構造をしているアラインされた時系列データを
複数含むようなデータフレームがあるような場合に便利です。

以下のスニペットで用法を確認してください：
*)
/// すべての数値列を特定の定数倍にします
df * 0.65

// すべての列にあるすべてのシリーズに関数を適用します
let conv x = min x 20.0
df |> Frame.mapRowValues (fun os -> conv $ os.As<float>())
   |> Frame.ofRows

// 各列の総和を計算して、結果を定数で除算します
Stats.sum df / 6.0
// 総和を各フレーム列の平均で除算します
Stats.sum df / Stats.mean df