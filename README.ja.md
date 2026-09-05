# MiniOrm

[#miniorm](#miniorm)

[English](./README.md)

> **これは本番用の ORM ではありません。** MiniOrm は、Entity Framework Core のコア内部実装をゼロから再実装した教育用プロジェクトです。`DbContext`、`DbSet<T>`、`ChangeTracker`、LINQ から SQL への変換、`SaveChanges` といった *EF Core の内部が実際にどう動いているのか* を、隠れた仕組みのないシンプルで読みやすい C# コードで示すことを目的としています。

EF Core を使っていて「`SaveChanges()` を呼ぶと実際には何が起きているのか?」「`.Where(x => x.Name.Contains(\"foo\"))` はどうやって SQL クエリに変換されるのか?」と疑問に思ったことがあるなら、このリポジトリはその小さく動作する実装を自分で作ることでその答えを示します。

## このプロジェクトが存在する理由

[#why-this-exists](#why-this-exists)

EF Core の実際のソースコードは大規模で、抽象化が多く、学習教材としてはたどりにくいものです。MiniOrm は同じアーキテクチャをわずか数個のファイル、合計約500行に凝縮しています。それぞれのファイルは意図的に実際の EF Core のコンポーネントを模しており、フレームワーク全体を読み解かなくてもコードを読むだけでパターンを直接理解できます。

| MiniOrm の型                   | EF Core の対応物                             | 学べる内容                                                                                                        |
| ----------------------------- | ------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------- |
| `DbContext`                   | `DbContext`                                 | Unit of Work — 接続、モデルを保持し、`SaveChanges` を調整する                                           |
| `DbSet<T>`                    | `DbSet<T>`                                  | リポジトリ + `IQueryable<T>` — テーブルへのクエリと変更操作の入り口                                       |
| `ChangeTracker`               | `ChangeTracker`                             | Identity Map — エンティティごとに型 + 主キーをキーとした追跡インスタンスを1つだけ保持                                  |
| `EntityEntry`                 | `InternalEntityEntry`                       | エンティティごとの状態マシン(`Added` / `Unchanged` / `Modified` / `Deleted`)と、スナップショット比較による変更検出 |
| `EntityType` / `ModelBuilder` | `IEntityType` / `ModelBuilder`              | 規約ベースの CLR クラスからテーブルへのマッピング(例: `Id` → 主キー、`Blog` → `Blogs`)                             |
| `QueryTranslator`             | `RelationalSqlTranslatingExpressionVisitor` | LINQ の**式木**をたどり、パラメータ化された SQL を生成する — `context.Blogs.Where(...)` の裏にある「魔法」の正体           |

## 実際にデモで示していること

[#what-it-actually-demonstrates](#what-it-actually-demonstrates)

デモを実行すると、EF Core がわずかなメソッド呼び出しの裏に隠しているライフサイクル全体をたどることができます。

1. **モデル構築** — `OnModelCreating` が `Blog` を `Blogs` テーブルにマッピングし、`EnsureCreated()` がリフレクションで取得したプロパティのメタデータから `CREATE TABLE` を発行します。
2. **変更追跡** — `context.Blogs.Add(...)` を呼び出してもデータベースには一切触れません。単に `ChangeTracker` に状態 `Added` の `EntityEntry` を登録するだけです。
3. **LINQ → SQL 変換** — `context.Blogs.Where(b => b.Title.Contains("EF"))` は式木を構築し、`QueryTranslator` がそれをノードごとにたどって `SELECT * FROM "Blogs" WHERE "Title" LIKE '%EF%'` を生成します。
4. **Identity Map** — クエリから返されたエンティティは `Unchanged` として追跡され、同じ行を2回クエリしても*同じ*オブジェクト参照が返されます。
5. **スナップショットベースの変更検出** — 追跡中のエンティティのプロパティを変更しても、それ自体では何も起こりません。`DetectChanges()` が元のスナップショットと現在の値を比較し、状態を `Modified` に切り替えます。これはまさに EF Core が毎回の `SaveChanges()` の前に自動的に行っていることです。
6. **SaveChanges** — 追跡中のすべてのエントリを1つのトランザクションでたどり、対応する `INSERT` / `UPDATE` / `DELETE` を生成した後、`AcceptAllChanges()` を呼び出して追跡状態をリセットします。

デモは各ステップで `ChangeTracker` の状態を出力するため、エンティティが `Added → Unchanged → Modified →(削除)` と実際に遷移していく様子をリアルタイムで確認できます。

## プロジェクト構成

[#project-structure](#project-structure)

```
MiniOrm/
├── src/
│   ├── MiniOrm/                  # ミニ ORM ライブラリ本体
│   │   ├── DbContext.cs          # Unit of Work: 接続、モデル、SaveChanges
│   │   ├── DbSet.cs              # IQueryable<T> の入り口(Add/Remove/クエリ)
│   │   ├── DbSetQueryProvider.cs # 変換済み SQL を実行し、エンティティを実体化
│   │   ├── QueryTranslator.cs    # 式木 → SQL への変換ビジター
│   │   ├── ChangeTracker.cs      # 追跡中の全エンティティの Identity Map
│   │   ├── EntityEntry.cs        # エンティティごとの状態 + スナップショットによる変更検出
│   │   ├── EntityType.cs         # リフレクションベースの CLR 型 → テーブルマッピング
│   │   └── ModelBuilder.cs       # Fluent API: modelBuilder.Entity<T>().ToTable(...)
│   └── MiniOrm.Demo/
│       └── Program.cs            # ステップごとのウォークスルー(下記参照)
├── Directory.Build.props/targets # 共有 MSBuild 設定
├── global.json                   # 固定された .NET SDK バージョン
├── MiniOrm.sln
└── LICENSE
```

## はじめに

[#getting-started](#getting-started)

### 前提条件

[#prerequisites](#prerequisites)

- [.NET SDK 8.0](https://dotnet.microsoft.com/download)(`global.json` でバージョンを固定)

### クローンしてデモを実行する

[#clone-and-run-the-demo](#clone-and-run-the-demo)

```
git clone https://github.com/ashishwebt/MiniOrm.git
cd MiniOrm
dotnet run --project src/MiniOrm.Demo
```

これによりローカルの SQLite データベース(`minidemo.db`)が構築され、上記で説明した EF Core のライフサイクルの各ステップがそのままコンソールに出力されます。追加のセットアップは不要です。

### ビルドのみ行う場合

[#build-only](#build-only)

```
dotnet restore
dotnet build
```

## 最小限の使用例

[#minimal-usage-example](#minimal-usage-example)

```
using MiniOrm;

public class Blog
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class BlogContext : DbContext
{
    public DbSet<Blog> Blogs => Set<Blog>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlite("Data Source=minidemo.db");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<Blog>().ToTable("Blogs");
}

using var context = new BlogContext();
context.EnsureCreated();

context.Blogs.Add(new Blog { Id = 1, Title = "Hello MiniOrm" });
context.SaveChanges();

var post = context.Blogs.First(b => b.Title.Contains("Hello"));
```

## 範囲と制限事項

[#scope-and-limitations](#scope-and-limitations)

これは意図的に最小限にとどめており、機能を網羅したものではありません。以下は**実装していません**(実装するつもりもありません): マイグレーション、リレーション/ナビゲーションプロパティ、遅延ロード、非同期クエリ実行、コネクションプーリング、クエリキャッシュ。これらはそれぞれ EF Core の実際のソースコードにおいてもかなり大きなトピックであり、MiniOrm は変更追跡型 ORM のコアとなるリクエスト/レスポンスのループのみを扱っています。

## コントリビュート

[#contributing](#contributing)

コードの教育的価値を高める貢献(より分かりやすいコメント、概念の追加説明、デモシナリオの追加など)を歓迎します。

1. リポジトリをフォークする
2. フィーチャーブランチを作成する(`git checkout -b feature/my-feature`)
3. 変更をコミットする
4. ブランチをプッシュしてプルリクエストを開く

## ライセンス

[#license](#license)

MIT — [LICENSE](https://github.com/ashishwebt/MiniOrm/blob/main/LICENSE) を参照してください。
