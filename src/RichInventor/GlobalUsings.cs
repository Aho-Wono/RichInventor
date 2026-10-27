// Inventor 名前空間には File / Path / Environment / Color といった、
// .NET の基本型と同じ名前の型がある。`using Inventor;` を書いたファイルでは
// これらが曖昧参照になりコンパイルが通らない。
//
// エイリアスは using ディレクティブより優先されるので、ここで一度だけ
// 「素で書いたらこっち」を宣言しておけばプロジェクト全体で解決できる。
// Inventor 側の型を使いたいときは Inventor.Path のように明示的に書くこと。

global using File = System.IO.File;
global using Path = System.IO.Path;
global using Directory = System.IO.Directory;
global using Environment = System.Environment;
