(**

F# Data Frameデザインノート
===========================

これはF# Data Frameライブラリの最初のバージョンで、
我々はどうすればデザインを向上させることができるか、
積極的に追求している最中です。
議論にうってつけの場所としては、
[GitHub上のissueリスト](https://github.com/BlueMountainCapital/Deedle/issues)
か、
[F# for Data and Machine Learning](http://fsharp.org/technical-groups/)
のメーリングリストのいずれかです(後者はより広範なトピックを対象にしています)。

現行のライブラリではほとんどの基礎的な機能が実装されていますが、
追加の(便利な)機能を簡単に実装することができるよう、
適切な内部「コア」機能も用意してあるつもりです。

ライブラリの実装時には、幅広い場面で有用な関数において使用されるような
**素朴(primitive)** かつ **基本的(fundamental)** な関数を少数用意するという
方針を採用しています(これらの関数は一般的には基本的なオブジェクトのメンバーとして
定義されます)。
また、一般的な操作を行う拡張メンバーや関数を
さらに増やしていけるといいと考えていますので、
是非このライブラリに貢献をお願いします！

## ライブラリの原則

 * **F# と C# で使いやすいこと** - このライブラリはF#とC#の両方で
   使用できるようにしたいと考えています。
   そのため、ほとんどの機能は拡張メンバーおよび
   (`Frame` および `Series`)モジュール関数として定義されます
   (拡張メンバーにはC#の`Extension`属性が指定されているため、
    それらはC#およびF# 3.1においてのみ利用可能です)。
   1つ異なる点として、拡張メンバーでは `KeyValuePair<K, V>` や `OptionalValue<T>`
   (ライブラリ内で定義されているC#フレンドリーな`struct`)が使用されているのに対して、
   関数ではタプルやF#の`option<T>`、省略型などが使用されています。

 * **行と列の対称性** - データフレーム内のデータは列のリストとして格納されるため、
   列方向にデータフレームを処理するほうがよいでしょう
   (列ベースのフレームを処理する関数も多数用意されています)。
   しかし`Frame<'TRowKey, `TColKey>` 型のデータには対称性が有り、
   列(シリーズ)と行の両方でアクセスできるような独自のインデックスを使用できます。
   また、`df.Columns` や `df.Rows` 経由で(ネストされた)シリーズのシリーズとして
   行や列にアクセスすることもできます。
   列キーは一般的には文字列(シリーズ名)になりますが、これは必須ではなく、
   `df.Transpose()` メソッドを呼ぶことにより、フレームを転置させることもできます。

 * **値無しおよびNaN** - データフレームには常に値無しが含まれうるものだと
   想定しているため、値無しを含む可能性のあるフレームまたはシリーズと、
   値無しを含まないものは、いずれも同じ型になっています。
   フレームやシリーズに対して実行可能な操作においては、
   値無しを適切に処理できるように設計されています。
   これらの操作では、値をキーによって明示的に読み取ろうとしない限り、
   基本的には値無しをスキップするようになっています。
   
   現在のバージョンでは、(数値に対する)`Double.NaN`や
   (`Nullable</T>`や参照型に対する)`null`といった特定の値が「値無し」と
   みなされるようになっています。
   つまり`Double.NaN`を含むシリーズを作成したとすると、
   この値が**値無し**となり、`Series.sum`のような集計関数を呼び出すと
   値が無視されるというわけです
   (`NaN`と**値無し**の両方に対応できるようにするべきかもしれませんが、
   何が最善の選択肢なのかという問いに対する答えは出ていません)。

 * **不変性(Immutability)** - シリーズは完全に不変なデータ型ですが、
   データフレームは限定的に可変性をサポートします。
   たとえば新しいシリーズを追加、削除、置換したりすることができます
   (ただしシリーズそのものを変更することは出来ません)。
   また、データフレームの行インデックスはほぼ不変ですが、
   空のデータフレームを作成した後に最初のシリーズを追加する場合に限って
   変更されることがあります。
   
   これらの処理は `?<-` 演算子で手軽に行うことが出来るため、
   リサーチ用のスクリプトを作成している場合などに再バインドする必要もありません。

## ライブラリの内部について

以下の型は(たいていの場合には)ユーザーが直接使用するようなものではありませんが、
あまり頻繁には変更されることがない「最小限の」コアを表すものです。
ライブラリを拡張させる場合にはこれらの型を使用することになるでしょう：

 * `IVector<'TValue>` は`Address`型を経由してアクセスすることができる
   `'TValue`型の値を含んだベクターです。
   具体的な実装としては単に`int`型のアドレスを持った配列とすればいいのですが、
   ライブラリではこれを抽象化しています。
   たとえば巨大なデータセットや、ストリームからデータを読み込むような遅延ベクター、
   (Cassandra等をデータソースとするような)仮想ベクターに対しては
   `int64`をインデックスとするような配列の配列が使用されることがあります。

   ベクターには、値無しを処理するという重要な役割があります。
   そのため、整数のベクターは `array<option<int>>` とみなすことができます
   (ただしこれが連続したメモリブロックに配置されるよう、独自の値型を定義しています)。
   値無しの処理はデータフレームにとって重要な項目であると判断したため、
   オプション値やNull許容の値を格納するのではなく、
   直接サポートするべきだということになりました。
   ライブラリの実装では、単純な最適化が行われています。
   もし値無しが全くない場合は単に`array<int>`が格納されます。

 * `VectorConstruction` は判別共用体で、ベクターの構造を表すものです。
   すべてのベクター型に対して、
   ベクターを生成する方法を定義している`IVectorBuilder`という
   インターフェイスの実装が用意されます
   (このインターフェイスには要素の再シャッフルやベクターの追加、部分区間の取得なども
    定義されています)。

 * `IIndex<'TKey>` はインデックス、つまりシリーズあるいはデータフレームのキーと
   ベクター内のアドレスとのマッピングを表します。
   単純なケースでは、これは指定された特定のキー(たとえば`string`や`DateTime`)に対応する、
   配列内の`int`型オフセットを返すような単なるハッシュテーブルになります。
   きわめて単純なインデックスとしては、アイデンティティ関数(未実装です！)によって
   `int`オフセットと`int`アドレスをマッピングするだけのものになります。
   シリーズやデータフレームの場合、これは単なるレコードのリストです。

Now, the following types are directly used:

 * `Series<'TKey, 'TValue>` represents a series of values `'TValue` indexed by an
   index `'TKey`. A series uses an abstract vector, index and vector builder, so it 
   should work with any data representation. A series provides some standard slicing 
   operators, projection, filtering etc. There are also some binary operators (multiply 
   by a scalar, add series, etc.) and addtional operations in the `Series` module.

 * `Frame<'TRowKey, 'TColumnKey>` represents a data frame with rows indexed using
   `TRowKey` (this could be `DateTime` or just ordinal numbers like `int`) and columns
   indexed by `TColumnKey` (typically a `string`). The data in the frame can be
   hetrogeneous (e.g. different types of values in different columns) and so accessing
   data is dynamic - but you can e.g. get a typed series.
   
   The operations available on the data frame include adding & removing series (which 
   aligns the new series according to the row index), joins (again - aligns the series) 
   etc. You can also get all rows as a series of (column) series and all columns as a 
   series of (row) series - they are available as extension methods and in the `Frame` module.

## Discussion and open questions

We're hoping that the design of the internals is now reasonable, but the end user API may
still be missing some useful functionality (let us know if you need some!) Here are a few
things that we discussed earlier and that we may still look into at some point:

 * **Time series vs. pivot table** - there is some mismatch between two possible
   interpretations and uses of the library. One is for time-series data (e.g. in finance)
   where one typically works with dates as row indices. More generally, you can see this
   as _continous_ index. It makes sense to do interpolation, sort the observations,
   align them, re-scale them etc. (Note that _continuous_ is stronger than _ordered_ -
   aside from time, the only continuous measure we can think of is distance-dependent
   series.)
   
   The other case is when we have some _discrete_ observations (perhaps a list of 
   records with customer data, a list of prices of different stock prices etc.) In this
   case, we need more "pivot table" functions etc.

   Although these two uses are quite different, we feel that it might make sense to use
   the same type for both (just with a different index). The problem is that this might
   make the API more complex. Although, if we can keep the distincion in the type, we can
   use F# 3.1 extension methods that extend just "discrete data frame" or "continous data 
   frame". However, for now all functions are available in `Frame`/`Series` module and 
   as extension methods that extend any type.

 * **Type provider** - we are thinking about using type providers to give some additional
   safety (like checking column names and types in a data frame). This is currently
   on the TODO list - we think we can do something useful here, although it will 
   certainly be limited. 

   The current idea is that you migth want to do some research/prototyping using a 
   dynamic data frame, but once you're done and have some more stable data, you should
   be able to write, say `DataFrame<"Open:float,Close:float">(dynamicDf)` and get a
   new typed data frame. 

If you have any comments regarding the topics above, please [submit an issue
on GitHub](https://github.com/BlueMountainCapital/Deedle/issues) or, if you
are interested in more actively contributing, join
the [F# for Data and Machine Learning](http://fsharp.org/technical-groups/) working
group. *)