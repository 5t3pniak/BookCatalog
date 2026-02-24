using BookCatalog.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace BookCatalog.Persistence;

public sealed class BookCatalogDbContext : DbContext
{
    public DbSet<Book> Books => Set<Book>();
    public DbSet<Author> Authors => Set<Author>();
    public DbSet<BookAuthor> BookAuthors => Set<BookAuthor>();
    public DbSet<BookTag> BookTags => Set<BookTag>();
    public DbSet<Tag> Tags => Set<Tag>();

    public BookCatalogDbContext(DbContextOptions<BookCatalogDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder model)
    {
        // Books
        model.Entity<Book>(e =>
        {
            e.ToTable("Books");
            e.HasKey(x => x.Slug);

            e.Property(x => x.Slug).HasMaxLength(200);
            e.Property(x => x.Title).HasMaxLength(500)
                .IsRequired()
                .UseCollation("Latin1_General_100_CI_AI");
            e.Property(x => x.Url).HasMaxLength(1000).IsRequired();
            e.Property(x => x.ThumbnailUrl).HasMaxLength(1000);
            e.Property(x => x.PrimaryAuthorSortKey).HasMaxLength(600)
                .UseCollation("Latin1_General_BIN");

            e.HasIndex(x => x.Title);
            e.HasIndex(x => x.PrimaryAuthorSortKey);
            e.HasIndex(x => x.IsDeleted);

            e.HasMany(x => x.BookAuthors).WithOne(x => x.Book).HasForeignKey(x => x.BookSlug);
            e.HasMany(x => x.BookTags).WithOne(x => x.Book).HasForeignKey(x => x.BookSlug);
        });

        // Authors
        model.Entity<Author>(e =>
        {
            e.ToTable("Authors");
            e.HasKey(x => x.Slug);

            e.Property(x => x.Slug).HasMaxLength(200);
            e.Property(x => x.Name).HasMaxLength(500).IsRequired();
            e.Property(x => x.SortKey).HasMaxLength(600)
                .UseCollation("Latin1_General_BIN");

            e.HasIndex(x => x.SortKey);
            e.HasIndex(x => x.Name);
            e.HasIndex(x => x.IsDeleted);

            e.HasMany(x => x.BookAuthors).WithOne(x => x.Author).HasForeignKey(x => x.AuthorSlug);
        });

       
        model.Entity<Tag>(e =>
        {
            e.ToTable("Tags");
            e.HasKey(x => x.Id);

            e.Property(x => x.Slug).HasMaxLength(200).IsRequired();
            e.Property(x => x.Name).HasMaxLength(300).IsRequired();
            e.Property(x => x.SortKey).HasMaxLength(300);

            e.HasIndex(x => new { x.Category, x.Slug }).IsUnique();
            e.HasIndex(x => new { x.Category, x.SortKey });
        });

    
        model.Entity<BookAuthor>(e =>
        {
            e.ToTable("BookAuthors");
            e.HasKey(x => new { x.BookSlug, x.AuthorSlug });

            e.Property(x => x.BookSlug).HasMaxLength(200);
            e.Property(x => x.AuthorSlug).HasMaxLength(200);

            e.HasIndex(x => new { x.AuthorSlug, x.BookSlug }); 
        });

        // BookTags
        model.Entity<BookTag>(e =>
        {
            e.ToTable("BookTags");
            e.HasKey(x => new { x.BookSlug, x.TagId });

            e.Property(x => x.BookSlug).HasMaxLength(200);

            e.HasIndex(x => new { x.TagId, x.BookSlug }); // filter by tag
        });
    }
}