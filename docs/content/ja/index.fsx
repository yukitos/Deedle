(*** hide ***)
#load "../../../bin/Deedle.fsx"
open System
open Deedle
let root = __SOURCE_DIRECTORY__ + "/../data/"

(**
Deedle: .NET用の探索的データライブラリ
======================================

<img src="http://www.bluemountaincapital.com/media/logo.gif" style="float:right;margin:10px" />

Deedleはデータおよび時系列操作、あるいは科学計算プログラミング用のライブラリで簡単に使用できます。
このライブラリは構造的データフレーム処理、整列済みあるいは未成列データ処理、時系列データ処理を
サポートしています。DeedleはF#やC#のインタラクティブコンソールを使用して行う
手探りのプログラミングに適するよう設計されていますが、コンパイル済みの.NETコードにおいても
活用できます。

ライブラリには複雑なインデクシングやスライシング、データの連結やアライメント、
値の無いデータに対する処理、グループ化や集計、統計など、多岐にわたるデータ操作が実装されています。

### タイタニック号の生存者を20行のコードで分析する

[タイタニック号のデータセット](http://www.kaggle.com/c/titanic-gettingStarted) を
`titanic` という名前のデータフレームとして読み込んであるとします
(このデータフレームにはint型の`Pclass`やbool型の`Survived`といった多数の列があります)。
そして乗船券の階級別に生存者の比率を計算してみましょう：

<div id="hp-snippeta">
*)
(*** define-output: sample ***)
// タイタニック号のデータを読み込み、「性別」でグループ化します
let titanic = Frame.ReadCsv(root + "titanic.csv").GroupRowsBy<int>("Pclass")

// 'Survived'列を取得して階級毎の生存者集を計算します
let byClass =
  titanic.GetColumn<bool>("Survived")
  |> Series.applyLevel fst (fun s ->
      // 'Survived'が'True'と'False'のデータを取得します
      series (Seq.countBy id s.Values))
  // 'Pclass'を行、'Died' と 'Survived' を列に持つようなフレームを作成します
  |> Frame.ofRows 
  |> Frame.sortRowsByKey
  |> Frame.indexColsWith ["Died"; "Survived"]

// タイタニック号の男性および女性の総数を持つ列を追加します
byClass?Total <- byClass?Died + byClass?Survived

// 結果をいい感じにパーセント表示するようなデータフレームを組み立てます
frame [ "死者 (%)" => round (byClass?Died / byClass?Total * 100.0)
        "生存者 (%)" => round (byClass?Survived / byClass?Total * 100.0) ]
(**
</div>

<style type="text/css">
.hp-table th, .hp-table td { width: 140px; }
.hp-table th:first-child, .hp-table td:first-child { width: 90px; }
</style>
<div class="hp-table">
*)

(*** include-it: sample ***)

(**
</div>

ここではまずデータを`Pclass`でグループ化して、`Survived`列をブール値のシリーズとして取得しています。
そして`applyLevel`を使用して各グループにまとめています。
この関数は旅行者の階級毎に特定の関数を呼び出します。
今回は生存者数と死者数をカウントしています。
そして適切なラベルを付けてフレームをソートし、
いい感じのまとめとなるような新しいデータフレームを組み立てています。

### Deedleの取得方法

 * ライブラリは [NuGetのDeedleサイト](https://www.nuget.org/packages/Deedle) からダウンロードできます。
   また [GitHub上のソースコード](https://github.com/BlueMountainCapital/Deedle/) を取得したり、
   [ソースコードをZIPファイルとしてダウンロード](https://github.com/BlueMountainCapital/Deedle/zipball/master)
   することもできます。
   コンパイル済みのバイナリを[ZIPファイルとしてダウンロード](https://github.com/BlueMountainCapital/Deedle/zipball/release)
   することもできます。

 * DeedleとF# Data、R 型プロバイダー、あるいはその他のF#製データサイエンスコンポーネントとを組み合わせたいのであれば、
   [FsLab パッケージ](https://www.nuget.org/packages/FsLab) を検討してみるとよいでしょう。
   Visual Studioを使用しているのであれば
   [FsLab プロジェクトテンプレート](http://visualstudiogallery.msdn.microsoft.com/45373b36-2a4c-4b6a-b427-93c7a8effddb)
   をインストールすることもおすすめです。

サンプルとドキュメント
----------------------

ライブラリには包括的なドキュメントが欠かせません。
チュートリアルや記事はいずれも [samplesフォルダ][samples] 内の
`*.fsx` ファイルから自動生成されています。
また、ライブラリの実装コードに記述されたMarkdownコメントを元にして、
APIリファレンスも自動生成されています。

 * [クイックスタートチュートリアル](tutorial.html) ではF#データライブラリの
   主要な機能の用法を紹介しています。
   まずはこちらから始めましょう。
   10分ほどでライブラリの用法を学習できます。

 * [データフレームの機能](frame.html) には一般的に使用されるデータフレームの機能について、
   さらに多くの例があります。
   たとえばスライシングや連結、グループ化、集計といった機能があります。

 * [シリーズの機能](series.html) では(株価のような)時系列データを処理する際に
   使用される機能について詳しく説明します。
   たとえばスライディングウィンドウやチャンク化、サンプリングや統計といった機能があります。

 * [フレームおよびシリーズ統計の計算](stats.html) では平均や分散、歪度などの統計指針を
   計算する方法について説明します。
   またこのチュートリアルではウィンドウの移動やウィンドウ統計の拡張も行います。

 * DeedleライブラリはF#とC#いずれにおいても使用できます。
   なるべくそれぞれの言語に倣ったAPIとなるようにしています。
   C#フレンドリーなAPIについては [DeedleをC#から使用する](csharpintro.html) の
   ページを参照してください。

ライブラリ内にあるすべての型やモジュール、関数から自動生成されたドキュメントが
[APIリファレンス](../reference/index.html) にあります。
また、すべてがドキュメント化されている主要なモジュールは以下の3つです：

 * [`Series` モジュール](../reference/deedle-seriesmodule.html) には
   各データシリーズや時系列の値を処理するための機能があります。
 * [`Frame` モジュール](../reference/deedle-framemodule.html) には
   `Series` モジュールと似た機能が多数ありますが、いずれもデータフレーム全体を対象とするものです。
 * [`Stats` モジュール](../reference/deedle-stats.html) には
   標準的な統計関数やウィンドウの移動など、多数の機能があります。
   このモジュールにはシリーズとフレーム両方を対象とする関数があります。

貢献方法および著作権
--------------------

プロジェクトは[GitHub][gh] 上でホストされており、
[Issuesの報告][issues] やプロジェクトのフォーク、プルリクエストの送信などを行うことができます。
公開用のAPIを新規に追加する場合はドキュメントとなるような [サンプル][samples] も
合わせて追加するようにしてください。
ライブラリの動作については [ライブラリの設計メモ](design.html) を参照されるとよいでしょう。

F# とデータサイエンスに関する一般的な話題に興味があるのであれば、
[F# data science and machine learning][fsharp-dwg] ワーキンググループに参加してみるとよいでしょう。
ここではF#用のデータサイエンスプロジェクトに関する活動を行っています。

ライブラリは [BlueMountain Capital](https://www.bluemountaincapital.com/) ならびに
共著者によって開発されています。
ライセンスはBSD ライセンスを採用しているため、
商用非商用問わず、変更および再配布することができます。
詳細についてはGitHubレポジトリの [ライセンスファイル][license] を参照してください。


  [samples]: https://github.com/blueMountainCapital/Deedle/tree/master/samples
  [gh]: https://github.com/blueMountainCapital/Deedle
  [issues]: https://github.com/blueMountainCapital/Deedle/issues
  [readme]: https://github.com/blueMountainCapital/Deedle/blob/master/README.md
  [license]: https://github.com/blueMountainCapital/Deedle/blob/master/LICENSE.md
  [fsharp-dwg]: http://fsharp.org/technical-groups/
*)