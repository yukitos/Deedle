(*** hide ***)
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
たとえば
In the previous sample, the code that performs aggregation is no longer
just a simple function like `Stats.mean`, but a lambda that takes `ds`,
which is of type `DataSegment<T>`. This type informs us whether the window
is complete or not. For example:
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
Sampling and resampling time series
-----------------------------------

Given a time series with high-frequency prices, sampling or resampling makes 
it possible to get time series with representative values at lower frequency.
The library uses the following terminology:

 - **Lookup** means that we find values at specified key; if a key is not
   available, we can look for value associated with the nearest smaller or 
   the nearest greater key.

 - **Resampling** means that we aggregate values values into chunks based
   on a specified collection of keys (e.g. explicitly provided times), or 
   based on some relation between keys (e.g. date times having the same date).

 - **Uniform resampling** is similar to resampling, but we specify keys by
   providing functions that generate a uniform sequence of keys (e.g. days),
   the operation also fills value for days that have no corresponding 
   observations in the input sequence.

Finally, the library also provides a few helper functions that are specifically
desinged for series with keys of types `DateTime` and `DateTimeOffset`.

### Lookup

Given a series `hf`, you can get a value at a specified key using `hf.Get(key)`
or using `hf |> Series.get key`. However, it is also possible to find values
for larger number of keys at once. The instance member for doing this
is `hf.GetItems(..)`. Moreover, both `Get` and `GetItems` take an optional
parameter that specifies the behavior when the exact key is not found.

Using the function syntax, you can use `Series.getAll` for exact key 
lookup and `Series.lookupAll` when you want more flexible lookup:
*)
// Generate a bit less than 24 hours of data with 13.7sec offsets
let mf = series <| stock1 (TimeSpan.FromSeconds(13.7)) 6300
// Generate keys for all minutes in 24 hours
let keys = [ for m in 0.0 .. 24.0*60.0-1.0 -> today.AddMinutes(m) ]

// Find value for a given key, or nearest greater key with value
mf |> Series.lookupAll keys Lookup.ExactOrGreater
// [fsi:val it : Series<DateTimeOffset,float> =]
// [fsi:  12:00:00 AM -> 20.07 ]
// [fsi:  12:01:00 AM -> 19.98 ]
// [fsi:  ...         -> ...   ]
// [fsi:  11:58:00 PM -> 19.03 ]
// [fsi:  11:59:00 PM -> <missing>        ]

// Find value for nearest smaller key
// (This returns value for 11:59:00 PM as well)
mf |> Series.lookupAll keys Lookup.ExactOrSmaller

// Find values for exact key 
// (This only works for the first key)
mf |> Series.lookupAll keys Lookup.Exact

(**
Lookup operations only return one value for each key, so they are useful for
quick sampling of large (or high-frequency) data. When we want to calculate
a new value based on multiple values, we need to use resampling.

### Resampling

Series supports two kinds of resamplings. The first kind is similar to lookup
in that we have to explicitly specify keys. The difference is that resampling
does not find just the nearest key, but all smaller or greater keys. For example:
*)

// For each key, collect values for greater keys until the 
// next one (chunk for 11:59:00 PM is empty)
mf |> Series.resample keys Direction.Forward

// For each key, collect values for smaller keys until the 
// previous one (the first chunk will be singleton series)
mf |> Series.resample keys Direction.Backward

// Aggregate each chunk of preceding values using mean
mf |> Series.resampleInto keys Direction.Backward 
  (fun k s -> Stats.mean s)

// Resampling is also available via the member syntax
mf.Resample(keys, Direction.Forward)
(**

The second kind of resampling is based on a projection from existing keys in 
the series. The operation then collects chunks such that the projection returns
equal keys. This is very similar to `Series.groupBy`, but resampling assumes 
that the projection preserves the ordering of the keys, and so it only aggregates
consequent keys.

The typical scenario is when you have time series with date time information
(here `DateTimeOffset`) and want to get information for each day (we use 
`DateTime` with empty time to represent dates):
*)

// Generate 2.5 months of data in 1.7 hour offsets
let ds = series <| stock1 (TimeSpan.FromHours(1.7)) 1000

// Sample by day (of type 'DateTime')
ds |> Series.resampleEquiv (fun d -> d.Date)

// Sample by day (of type 'DateTime')
ds.ResampleEquivalence(fun d -> d.Date)
(**
The same operation can be easily implemented using `Series.chunkWhile`, but as
it is often used in the context of sampling, it is included in the library as a
primitive. Moreover, we'll see that it is closely related to uniform resampling.

Note that the resulting series has different type of keys than the source. The
source has keys `DateTimeOffset` (representing date with time) while the resulting
keys are of the type returned by the projection (here, `DateTime` representing just
dates).

### Uniform resampling

In the previous section, we looked at `resampleEquiv`, which is useful if you want
to sample time series by keys with "lower resolution" - for example, sample date time
observations by date. However, the function discussed in the previous section only
generates values for which there are keys in the input sequence - if there is no
observation for an entire day, then the day will not be included in the result.

If you want to create sampling that assigns value to each key in the range specified
by the input sequence, then you can use _uniform resampling_.

The idea is that uniform resampling applies the key projection to the smallest and
greatest key of the input (e.g. gets date of the first and last observation) and then
it generates all keys in the projected space (e.g. all dates). Then it picks the
best value for each of the generated key.
*)

// Create input data with non-uniformly distributed keys
// (1 value for 10/3, three for 10/4 and two for 10/6)
let days =
  [ "10/3/2013 12:00:00"; "10/4/2013 15:00:00" 
    "10/4/2013 18:00:00"; "10/4/2013 19:00:00"
    "10/6/2013 15:00:00"; "10/6/2013 21:00:00" ]
let nu = 
  stock1 (TimeSpan(24,0,0)) 10 |> series
  |> Series.indexWith days |> Series.mapKeys DateTimeOffset.Parse

// Generate uniform resampling based on dates. Fill
// missing chunks with nearest smaller observations.
let sampled =
  nu |> Series.resampleUniform Lookup.ExactOrSmaller 
    (fun dt -> dt.Date) (fun dt -> dt.AddDays(1.0))

// Same thing using the C#-friendly member syntax
// (Lookup.ExactOrSmaller is the default value)
nu.ResampleUniform((fun dt -> dt.Date), (fun dt -> dt.AddDays(1.0)))

// Turn into frame with multiple columns for each day
// (to format the result in a readable way)
sampled 
|> Series.mapValues Series.indexOrdinally
|> Frame.ofRows
// [fsi:val it : Frame<DateTime,int> =]
// [fsi:             0      1          2                ]
// [fsi:10/3/2013 -> 21.45  <missing>  <missing>        ]
// [fsi:10/4/2013 -> 21.63  19.83      17.51]
// [fsi:10/5/2013 -> 17.51  <missing>  <missing>        ]
// [fsi:10/6/2013 -> 18.80  20.93      <missing>        ]

(**
To perform the uniform resampling, we need to specify how to project (resampled) keys
from original keys (we return the `Date`), how to calculate the next key (add 1 day)
and how to fill missing values.

After performing the resampling, we turn the data into a data frame, so that we can 
nicely see the results. The individual chunks have the actual observation times as keys,
so we replace those with just integers (using `Series.indexOrdinal`). The result contains
a simple ordered row of observations for each day.

The important thing is that there is an observation for each day - even for for 10/5/2013
which does not have any corresponding observations in the input. We call the resampling
function with `Lookup.ExactOrSmaller`, so the value 17.51 is picked from the last observation
of the previous day (`Lookup.ExactOrGreater` would pick 18.80 and `Lookup.Exact` would give
us an empty series for that date).

### Sampling time series

Perhaps the most common sampling operation that you might want to do is to sample time series
by a specified `TimeSpan`. Although this can be easily done by using some of the functions above,
the library provides helper functions exactly for this purpose:

*)
// Generate 1k observations with 1.7 hour offsets
let pr = series <| stock1 (TimeSpan.FromHours(1.7)) 1000

// Sample at 2 hour intervals; 'Backward' specifies that
// we collect all previous values into a chunk.
pr |> Series.sampleTime (TimeSpan(2, 0, 0)) Direction.Backward

// Same thing using member syntax - 'Backward' is the dafult
pr.Sample(TimeSpan(2, 0, 0))

// Get the most recent value, sampled at 2 hour intervals
pr |> Series.sampleTimeInto
  (TimeSpan(2, 0, 0)) Direction.Backward Series.lastValue

(**
<a name="stats"></a>
Calculations and statistics
---------------------------

In the final section of this tutorial, we look at writing some calculations over time series. Many of the
functions demonstrated here can be also used on unordered data frames and series.

### Shifting and differences

First of all, let's look at functions that we need when we need to compare subsequent values in
the series. We already demonstrated how to do this using `Series.pairwise`. In many cases,
the same thing can be done using an operation that operates over the entire series.

The two useful functions here are:

 - `Series.diff` calcualtes the difference between current and n-_th_  previous element
 - `Series.shift` shifts the values of a series by a specified offset

The following snippet illustrates how both functions work:
*)
// Generate sample data with 1.7 hour offsets
let sample = series <| stock1 (TimeSpan.FromHours(1.7)) 6

// Calculates: new[i] = s[i] - s[i-1]
let diff1 = sample |> Series.diff 1
// Diff in the opposite direction
let diffM1 = sample |> Series.diff -1

// Shift series values by 1
let shift1 = sample |> Series.shift 1

// Align all results in a frame to see the results
let df = 
  [ "Shift +1" => shift1 
    "Diff +1" => diff1 
    "Diff" => sample - shift1 
    "Orig" => sample ] |> Frame.ofColumns 
// [fsi:val it : Frame<DateTimeOffset,string> =]
// [fsi:                 Diff       Diff +1    Orig   Shift +1         ]
// [fsi:  12:00:00 AM -> <missing>  <missing>  21.73  <missing>        ]
// [fsi:   1:42:00 AM ->  1.73       1.73      23.47  21.73 ]
// [fsi:   3:24:00 AM -> -0.83      -0.83      22.63  23.47 ]
// [fsi:   5:06:00 AM ->  2.37       2.37      25.01  22.63 ]
// [fsi:   6:48:00 AM -> -1.57      -1.57      23.43  25.01 ]
// [fsi:   8:30:00 AM ->  0.09       0.09      23.52  23.43 ]

(**
In the above snippet, we first calcluate difference using the `Series.diff` function.
Then we also show how to do that using `Series.shift` and binary operator applied
to two series (`sample - shift`). The following section provides more details. 
So far, we also used the functional notation (e.g. `sample |> Series.diff 1`), but
all operations can be called using the member syntax - very often, this gives you
a shorter syntax. This is also shown in the next few snippets.

### Operators and functions

Time series also supports a large number of standard F# functions such as `log` and `abs`.
You can also use standard numerical operators to apply some operation to all elements
of the series. 

Because series are indexed, we can also apply binary operators to two series. This 
automatically aligns the series and then applies the operation on corresponding elements.

*)

// Subtract previous value from the current value
sample - sample.Shift(1)

// Calculate logarithm of such differences
log (sample - sample.Shift(1))

// Calculate square of differences
sample.Diff(1) ** 2.0

// Calculate average of value and two immediate neighbors
(sample.Shift(-1) + sample + sample.Shift(2)) / 3.0

// Get absolute value of differences
abs (sample - sample.Shift(1))

// Get absolute value of distance from the mean
abs (sample - (Stats.mean sample))

(**
The time series library provides a large number of functions that can be applied in this
way. These include trigonometric functions (`sin`, `cos`, ...), rounding functions
(`round`, `floor`, `ceil`), exponentials and logarithms (`exp`, `log`, `log10`) and more.
In general, whenever there is a built-in numerical F# function that can be used on 
standard types, the time series library should support it too.

However, what can you do when you write a custom function to do some calculation and
want to apply it to all series elements? Let's have a look:
*)

// Truncate value to interval [-1.0, +1.0]
let adjust v = min 1.0 (max -1.0 v)

// Apply adjustment to all function
adjust $ sample.Diff(1)

// The $ operator is a shorthand for
sample.Diff(1) |> Series.mapValues adjust

(**
In general, the best way to apply custom functions to all values in a series is to 
align the series (using either `Series.join` or `Series.joinAlign`) into a single series
containing tuples and then apply `Series.mapValues`. The library also provides the `$` operator
that simplifies the last step - `f $ s` applies the function `f` to all values of the series `s`.

### Data frame operations

Finally, many of the time series operations demonstrated above can be applied to entire
data frames as well. This is particularly useful if you have data frame that contains multiple
aligned time series of similar structure (for example, if you have multiple stock prices or 
open-high-low-close values for a given stock). 

The following snippet is a quick overview of what you can do:
*)
/// Multiply all numeric columns by a given constant
df * 0.65

// Apply function to all columns in all series
let conv x = min x 20.0
df |> Frame.mapRowValues (fun os -> conv $ os.As<float>())
   |> Frame.ofRows

// Sum each column and divide results by a constant
Stats.sum df / 6.0
// Divide sum by mean of each frame column
Stats.sum df / Stats.mean df
