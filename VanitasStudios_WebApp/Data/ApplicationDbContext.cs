using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;
using VanitasStudios_WebApp.Models;
using Microsoft.AspNetCore.Identity;

namespace VanitasStudios_WebApp.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public virtual DbSet<Comment> Comments { get; set; }

        public virtual DbSet<Content> Contents { get; set; }

        public virtual DbSet<Evaluate> Evaluates { get; set; }

        public virtual DbSet<Image> Images { get; set; }

        public virtual DbSet<Promotion> Promotions { get; set; }

        public virtual DbSet<Section> Sections { get; set; }

        public virtual DbSet<Tag> Tags { get; set; }

        public virtual DbSet<Video> Videos { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Comment>(entity =>
            {
                entity.HasKey(e => e.IdComm).HasName("PK__Comment__560C251E2B77EB82");

                entity.ToTable("Comment");

                entity.Property(e => e.IdComm).HasColumnName("ID_Comm");
                entity.Property(e => e.AnswerId).HasColumnName("Answer_ID");
                entity.Property(e => e.CommText)
                    .HasMaxLength(2000)
                    .HasColumnName("Comm_Text");
                entity.Property(e => e.CommentUserId).HasColumnName("Comment_User_ID");
                entity.Property(e => e.ContentId).HasColumnName("Content_ID");
                entity.Property(e => e.DataPub)
                    .HasPrecision(0)
                    .HasDefaultValueSql("(getdate())")
                    .HasColumnName("Data_Pub");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.Comments)
                    .HasForeignKey(d => d.CommentUserId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_Comment_User_ID");

                entity.HasOne(d => d.Answer).WithMany(p => p.InverseAnswer)
                    .HasForeignKey(d => d.AnswerId)
                    .HasConstraintName("FK_Answer_ID");

                entity.HasOne(d => d.Content).WithMany(p => p.Comments)
                    .HasForeignKey(d => d.ContentId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Content_ID");
            });

            modelBuilder.Entity<Content>(entity =>
            {
                entity.HasKey(e => e.IdC).HasName("PK__Content__B87EA50961BA4A46");

                entity.ToTable("Content");

                entity.Property(e => e.IdC).HasColumnName("ID_C");
                entity.Property(e => e.DataEdit)
                    .HasPrecision(0)
                    .HasColumnName("Data_Edit");
                entity.Property(e => e.DataPub)
                    .HasPrecision(0)
                    .HasDefaultValueSql("(getdate())")
                    .HasColumnName("Data_Pub");
                entity.Property(e => e.DescC)
                    .HasMaxLength(500)
                    .HasColumnName("Desc_C");
                entity.Property(e => e.EditorId).HasColumnName("Editor_ID");
                entity.Property(e => e.Title).HasMaxLength(255);
                entity.Property(e => e.TypeC)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasDefaultValue("articolo")
                    .HasColumnName("Type_C");

                entity.HasOne(d=> d.Editor)
                    .WithMany(p => p.Contents)
                    .HasForeignKey(d => d.EditorId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_Editor");

                entity.HasMany(d => d.TagOrds).WithMany(p => p.ContentOrds)
                    .UsingEntity<Dictionary<string, object>>(
                        "Order",
                        r => r.HasOne<Tag>().WithMany()
                            .HasForeignKey("TagOrdId")
                            .OnDelete(DeleteBehavior.ClientSetNull)
                            .HasConstraintName("FK_Tag_Ord_ID"),
                        l => l.HasOne<Content>().WithMany()
                            .HasForeignKey("ContentOrdId")
                            .OnDelete(DeleteBehavior.ClientSetNull)
                            .HasConstraintName("FK_Content_Ord_ID"),
                        j =>
                        {
                            j.HasKey("ContentOrdId", "TagOrdId").HasName("PK__Order__16EB5FFAF718821E");
                            j.ToTable("Order");
                            j.IndexerProperty<int>("ContentOrdId").HasColumnName("Content_Ord_ID");
                            j.IndexerProperty<int>("TagOrdId").HasColumnName("Tag_Ord_ID");
                        });
            });

            modelBuilder.Entity<Evaluate>(entity =>
            {
                entity.HasKey(e => new { e.UserLikeId, e.CommLikeId }).HasName("PK__Evaluate__1F28091D4ECADA78");

                entity.ToTable("Evaluate");

                entity.Property(e => e.UserLikeId).HasColumnName("User_Like_ID");
                entity.Property(e => e.CommLikeId).HasColumnName("Comm_Like_ID");
                entity.Property(e => e.IsLike).HasColumnName("isLike");

                entity.HasOne(d => d.UserLike)
                    .WithMany(p => p.Evaluates)
                    .HasForeignKey(d => d.UserLikeId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_User_Like_ID");

                entity.HasOne(d => d.CommLike).WithMany(p => p.Evaluates)
                    .HasForeignKey(d => d.CommLikeId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Comm_Like_ID");
            });

            modelBuilder.Entity<Image>(entity =>
            {
                entity.HasKey(e => e.IdI).HasName("PK__Image__B87EA503141B6819");

                entity.ToTable("Image");

                entity.Property(e => e.IdI).HasColumnName("ID_I");
                entity.Property(e => e.ImageUrl)
                    .HasMaxLength(100)
                    .IsUnicode(false)
                    .HasColumnName("Image_Url");
                entity.Property(e => e.IsThumbnail).HasColumnName("Is_Thumbnail");
                entity.Property(e => e.SectionImageId).HasColumnName("Section_Image_ID");

                entity.HasOne(d => d.SectionImage).WithMany(p => p.Images)
                    .HasForeignKey(d => d.SectionImageId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Section_Image_ID");
            });

            modelBuilder.Entity<Promotion>(entity =>
            {
                entity.HasKey(e => e.IdPromotion).HasName("PK__Promotio__ECECECBEA1BEC634");

                entity.ToTable("Promotion");

                entity.Property(e => e.IdPromotion).HasColumnName("ID_Promotion");
                entity.Property(e => e.AdminPromoterId).HasColumnName("Admin_Promoter_ID");
                entity.Property(e => e.DataPromotion)
                    .HasDefaultValueSql("(getdate())")
                    .HasColumnName("Data_Promotion");
                entity.Property(e => e.PromotedId).HasColumnName("Promoted_ID");

                entity.HasOne(d => d.Promoted)
                    .WithMany()
                    .HasForeignKey(d => d.PromotedId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Promoted_ID");

                entity.HasOne(d => d.AdminPromoter)
                    .WithMany()
                    .HasForeignKey(d => d.AdminPromoterId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Admin_Promoter_ID");
            });

            modelBuilder.Entity<Section>(entity =>
            {
                entity.HasKey(e => e.IdS).HasName("PK__Section__B87EA5193321E3F5");

                entity.ToTable("Section");

                entity.Property(e => e.IdS).HasColumnName("ID_S");
                entity.Property(e => e.ContentSId).HasColumnName("Content_S_ID");
                entity.Property(e => e.OrderNum).HasColumnName("Order_num");
                entity.Property(e => e.SectionText).HasColumnName("Section_Text");
                entity.Property(e => e.Title).HasMaxLength(250);

                entity.HasOne(d => d.ContentS).WithMany(p => p.Sections)
                    .HasForeignKey(d => d.ContentSId)
                    .HasConstraintName("FK_Content_S_ID");
            });

            modelBuilder.Entity<Tag>(entity =>
            {
                entity.HasKey(e => e.IdT).HasName("PK__Tag__B87EA51889DF0D54");

                entity.ToTable("Tag");

                entity.Property(e => e.IdT).HasColumnName("ID_T");
                entity.Property(e => e.TagName)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasColumnName("Tag_Name");
                entity.Property(e => e.TypeT)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasDefaultValue("articolo")
                    .HasColumnName("Type_T");
            });

            modelBuilder.Entity<Video>(entity =>
            {
                entity.HasKey(e => e.IdV).HasName("PK__Video__B87EA516CAC63B2A");

                entity.ToTable("Video");

                entity.HasIndex(e => e.ImageVideoId, "UQ__Video__57417FA3F6E793E4").IsUnique();

                entity.Property(e => e.IdV).HasColumnName("ID_V");
                entity.Property(e => e.ImageVideoId).HasColumnName("Image_Video_ID");
                entity.Property(e => e.SectionVideoId).HasColumnName("Section_Video_ID");
                entity.Property(e => e.VideoUrl)
                    .HasMaxLength(100)
                    .IsUnicode(false)
                    .HasColumnName("Video_Url");

                entity.HasOne(d => d.ImageVideo).WithOne(p => p.Video)
                    .HasForeignKey<Video>(d => d.ImageVideoId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Image_Video_ID");

                entity.HasOne(d => d.SectionVideo).WithMany(p => p.Videos)
                    .HasForeignKey(d => d.SectionVideoId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Section_Video_ID");
            });
        }
    }
}
