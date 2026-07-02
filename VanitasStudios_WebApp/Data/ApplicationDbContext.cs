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

        public virtual DbSet<CommentLike> CommentLikes { get; set; }

        public virtual DbSet<Media> Media { get; set; }

        public virtual DbSet<Promotion> Promotions { get; set; }

        public virtual DbSet<Section> Sections { get; set; }

        public virtual DbSet<Tag> Tags { get; set; }

        public virtual DbSet<SearchHistory> SearchHistories { get; set; }

        public virtual DbSet<StatisticalWeights> StatisticalWeights { get; set; }

        public virtual DbSet<TagSynonym> TagSynonyms { get; set; }

        public virtual DbSet<ContentTag> ContentTags { get; set; }
        public virtual DbSet<AdminLog> AdminLogs { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Comment>(entity =>
            {
                // 1. Mappatura sulla NUOVA tabella al plurale
                entity.ToTable("Comments");

                // 2. Definizione della Chiave Primaria con un nome pulito per l'indice
                entity.HasKey(e => e.Id).HasName("PK_Comments");

                // 3. Allineamento perfetto dei vecchi nomi colonna ai nuovi nomi lineari
                entity.Property(e => e.Id).HasColumnName("Id");
                entity.Property(e => e.ParentCommentId).HasColumnName("ParentCommentId");

                entity.Property(e => e.Text)
                    .HasMaxLength(2000)
                    .HasColumnName("Text")
                    .IsRequired(); // Assicura che la colonna sia NOT NULL

                entity.Property(e => e.UserId).HasColumnName("UserId");
                entity.Property(e => e.ContentId).HasColumnName("ContentId");

                // Manteniamo la tua precisione e il recupero automatico della data su SQL Server
                entity.Property(e => e.CreatedAt)
                    .HasPrecision(0)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP")
                    .HasColumnName("CreatedAt");

                // 4. Relazione verso l'Utente (Chi scrive il commento)
                entity.HasOne(d => d.User)
                    .WithMany(p => p.Comments)
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.Restrict) // Manteniamo il tuo Restrict originale, evita crash su SQL Server
                    .HasConstraintName("FK_Comments_Users");

                // 5. Relazione ricorsiva (Le risposte ai commenti)
                entity.HasOne(d => d.ParentComment)
                    .WithMany(p => p.Replies)
                    .HasForeignKey(d => d.ParentCommentId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Comments_ParentComments");

                // 6. Relazione verso l'Articolo (Content)
                entity.HasOne(d => d.Content)
                    .WithMany(p => p.Comments) // Assicurati che in Content ci sia o ci sarà la ICollection<Comment> Comments
                    .HasForeignKey(d => d.ContentId)
                    .OnDelete(DeleteBehavior.ClientSetNull) // Manteniamo la tua impostazione protettiva
                    .HasConstraintName("FK_Comments_Contents");
            });

            modelBuilder.Entity<Content>(entity =>
            {
                // 1. Mappatura sulla tabella al plurale
                entity.ToTable("Contents");

                // 2. Chiave primaria pulita
                entity.HasKey(e => e.Id).HasName("PK_Contents");

                // 3. Mappatura colonne e allineamento nomi
                entity.Property(e => e.Id).HasColumnName("Id");

                entity.Property(e => e.Title)
                    .HasMaxLength(255)
                    .HasColumnName("Title")
                    .IsRequired();

                entity.Property(e => e.Slug)
                    .HasMaxLength(255)
                    .HasColumnName("Slug")
                    .IsRequired();

                entity.Property(e => e.Description)
                    .HasColumnName("Description")
                    .IsRequired(false); // Può essere null se non metti un abstract immediato

                entity.Property(e => e.CoverImageUrl)
                    .HasMaxLength(512)
                    .HasColumnName("CoverImageUrl")
                    .IsRequired(false);

                entity.Property(e => e.IsPinned)
                    .HasColumnName("IsPinned")
                    .HasDefaultValue(false);

                // Gestione dell'enum per lo stato (Bozza, Pubblico, Eliminato) salvato come intero sul DB
                entity.Property(e => e.PublishState)
                    .HasColumnName("PublishState")
                    .IsRequired();

                entity.Property(e => e.GlobalScore)
                    .HasColumnName("GlobalScore")
                    .HasDefaultValue(0.0f);

                // Manteniamo la tua logica nativa di SQL Server per la data di creazione
                entity.Property(e => e.CreatedAt)
                    .HasPrecision(0)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP")
                    .HasColumnName("CreatedAt");

                entity.Property(e => e.UpdatedAt)
                    .HasPrecision(0)
                    .HasColumnName("UpdatedAt")
                    .IsRequired(false);

                entity.Property(e => e.EliminatedAt)
                    .HasPrecision(0)
                    .HasColumnName("EliminatedAt")
                    .IsRequired(false);

                entity.Property(e => e.AuthorId).HasColumnName("AuthorId");

                // 4. Relazione uno-a-molti verso l'Autore (ApplicationUser)
                entity.HasOne(d => d.Author)
                    .WithMany(p => p.AuthoredArticles)
                    .HasForeignKey(d => d.AuthorId)
                    .OnDelete(DeleteBehavior.Restrict) // Manteniamo la tua protezione originale
                    .HasConstraintName("FK_Contents_Users");

                // NOTA SUI TAG: Non c'è più la configurazione complessa di prima ("Order") qui dentro!
                // Avendo trasformato la relazione in un'entità esplicita (ContentTag), la configurazione 
                // delle chiavi e delle join la faremo direttamente nel blocco dedicato a modelBuilder.Entity<ContentTag>().
            });

            modelBuilder.Entity<CommentLike>(entity =>
            {
                // 1. Definiamo la chiave composta (User + Commento) e diamo un nome pulito al vincolo SQL
                entity.HasKey(e => new { e.UserId, e.CommentId }).HasName("PK_CommentLikes");

                // 2. Diciamo a EF di mappare la classe sulla NUOVA tabella "CommentLikes" (addio "Evaluate")
                entity.ToTable("CommentLikes");

                // 3. Mappiamo le proprietà sulle colonne SQL con nomi puliti e standard
                entity.Property(e => e.UserId).HasColumnName("UserId");
                entity.Property(e => e.CommentId).HasColumnName("CommentId");
                entity.Property(e => e.IsLike).HasColumnName("IsLike");

                // 4. Configurazione della relazione verso l'Utente
                entity.HasOne(d => d.User)
                    .WithMany(p => p.GivenCommentLikes)
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.Cascade) // Se un utente viene cancellato, saltano i suoi like (più pulito)
                    .HasConstraintName("FK_CommentLikes_Users");

                // 5. Configurazione della relazione verso il Commento
                entity.HasOne(d => d.Comment)
                    .WithMany(p => p.CommentLikes) 
                    .HasForeignKey(d => d.CommentId)
                    .OnDelete(DeleteBehavior.Cascade) // Se un commento viene cancellato, saltano i suoi like
                    .HasConstraintName("FK_CommentLikes_Comments");
            });

            modelBuilder.Entity<Promotion>(entity =>
            {
                // 1. Mappatura sulla tabella al plurale
                entity.ToTable("Promotions");

                // 2. Chiave primaria
                entity.HasKey(e => e.Id).HasName("PK_Promotions");

                // 3. Allineamento colonne
                entity.Property(e => e.Id).HasColumnName("Id");
                entity.Property(e => e.PromotedId).HasColumnName("PromotedId");
                entity.Property(e => e.PromoterId).HasColumnName("PromoterId");

                entity.Property(e => e.PromotedAt)
                    .HasPrecision(0)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP") // Se ti dimentichi la data, ci pensa SQL Server
                    .HasColumnName("PromotedAt");

                // 4. Configurazione Relazione 1: L'utente promosso
                entity.HasOne(d => d.Promoted)
                    .WithMany(p => p.ReceivedPromotions)
                    .HasForeignKey(d => d.PromotedId)
                    .OnDelete(DeleteBehavior.Restrict) // Fondamentale per evitare conflitti su SQL Server
                    .HasConstraintName("FK_Promotions_Users_Promoted");

                // 5. Configurazione Relazione 2: L'amministratore promoter
                entity.HasOne(d => d.Promoter)
                    .WithMany(p => p.GrantedPromotions)
                    .HasForeignKey(d => d.PromoterId)
                    .OnDelete(DeleteBehavior.Restrict) // Fondamentale per evitare conflitti su SQL Server
                    .HasConstraintName("FK_Promotions_Users_Promoter");
            });

            modelBuilder.Entity<Section>(entity =>
            {
                // 1. Mappatura sulla tabella al plurale
                entity.ToTable("Sections");

                // 2. Definizione della Chiave Primaria
                entity.HasKey(e => e.Id).HasName("PK_Sections");

                // 3. Allineamento dei nomi delle colonne
                entity.Property(e => e.Id).HasColumnName("Id");

                entity.Property(e => e.Title)
                    .HasMaxLength(255)
                    .HasColumnName("Title")
                    .IsRequired(false); // Consente titoli nulli per blocchi di testo continui

                entity.Property(e => e.HtmlText)
                    .HasColumnName("Text")
                    .IsRequired();

                entity.Property(e => e.Order)
                    .HasColumnName("Order")
                    .HasDefaultValue(0);

                entity.Property(e => e.ContentId).HasColumnName("ContentId");

                // 4. Configurazione della relazione verso l'Articolo padre
                entity.HasOne(d => d.Content)
                    .WithMany(p => p.Sections)
                    .HasForeignKey(d => d.ContentId)
                    .OnDelete(DeleteBehavior.Cascade) // Se elimini l'articolo, è giusto che saltino tutte le sue sezioni di testo!
                    .HasConstraintName("FK_Sections_Contents");
            });

            modelBuilder.Entity<Tag>(entity =>
            {
                // 1. Mappatura sulla nuova tabella al plurale
                entity.ToTable("Tags");

                // 2. Definizione della Chiave Primaria
                entity.HasKey(e => e.Id).HasName("PK_Tags");

                // 3. Allineamento delle colonne SQL alle nuove proprietà C#
                entity.Property(e => e.Id).HasColumnName("Id");

                entity.Property(e => e.Name)
                    .HasMaxLength(100)
                    .HasColumnName("Name")
                    .IsRequired();

                entity.Property(e => e.CategoryGroup)
                    .HasMaxLength(100)
                    .HasColumnName("CategoryGroup")
                    .IsRequired(false);

                // NOTA: La colonna Type_C / TypeT è stata completamente rimossa.
                // La vecchia relazione complessa con la tabella "Order" è sparita anche da qui, 
                // perché ora è gestita interamente dal blocco modelBuilder.Entity<ContentTag>() che abbiamo scritto prima.
            });

            modelBuilder.Entity<Media>(entity =>
            {
                // 1. Mappatura sulla nuova tabella unica
                entity.ToTable("Media");

                // 2. Chiave primaria
                entity.HasKey(e => e.Id).HasName("PK_Media");

                // 3. Mappatura colonne
                entity.Property(e => e.Id).HasColumnName("Id");

                entity.Property(e => e.Url)
                    .HasMaxLength(512)
                    .HasColumnName("Url")
                    .IsRequired();

                entity.Property(e => e.Type)
                    .HasColumnName("Type")
                    .IsRequired();

                entity.Property(e => e.Caption)
                    .HasMaxLength(255)
                    .HasColumnName("Caption")
                    .IsRequired(false);

                entity.Property(e => e.IsThumbnail)
                    .HasColumnName("IsThumbnail")
                    .HasDefaultValue(false);

                entity.Property(e => e.Order)
                    .HasColumnName("Order")
                    .HasDefaultValue(0);

                entity.Property(e => e.ReferenceCount)
                    .HasColumnName("ReferenceCount")
                    .HasDefaultValue(1)
                    .IsRequired();

                entity.Property(e => e.SectionId).HasColumnName("SectionId");

                // 4. Relazione verso la Sezione (Se salta la sezione di testo, è giusto che saltino i suoi media associati)
                entity.HasOne(d => d.Section)
                    .WithMany(p => p.MediaElements)
                    .HasForeignKey(d => d.SectionId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_Media_Sections");
            });

            modelBuilder.Entity<ContentTag>(entity =>
            {
                // 1. Definiamo la chiave composta
                entity.HasKey(e => new { e.ContentId, e.TagId }).HasName("PK_ContentTags");

                // 2. Mappatura sulla tabella fisica
                entity.ToTable("ContentTags");

                entity.Property(e => e.ContentId).HasColumnName("ContentId");
                entity.Property(e => e.TagId).HasColumnName("TagId");

                // Configurazione del peso algoritmico
                entity.Property(e => e.Weight)
                    .HasColumnName("Weight")
                    .HasDefaultValue(0.0f)
                    .IsRequired();

                // 3. Relazione verso il Contenuto (Articolo)
                entity.HasOne(d => d.Content)
                    .WithMany(p => p.ContentTags)
                    .HasForeignKey(d => d.ContentId)
                    .OnDelete(DeleteBehavior.Cascade) // Se elimini un articolo, saltano i suoi collegamenti ai tag
                    .HasConstraintName("FK_ContentTags_Contents");

                // 4. Relazione verso il Tag
                entity.HasOne(d => d.Tag)
                    .WithMany(p => p.ContentTags) 
                    .HasForeignKey(d => d.TagId)
                    .OnDelete(DeleteBehavior.Cascade) // Se elimini un tag globale, salta il collegamento su tutti gli articoli
                    .HasConstraintName("FK_ContentTags_Tags");
            });

            modelBuilder.Entity<TagSynonym>(entity =>
            {
                // 1. Nome della tabella fisica
                entity.ToTable("TagSynonyms");

                // 2. Chiave primaria
                entity.HasKey(e => e.Id).HasName("PK_TagSynonyms");

                // 3. Mappatura colonne
                entity.Property(e => e.Id).HasColumnName("Id");

                entity.Property(e => e.SynonymName)
                    .HasMaxLength(100)
                    .HasColumnName("SynonymName")
                    .IsRequired();

                entity.Property(e => e.TagId).HasColumnName("TagId");

                // 4. Configurazione della relazione (Se elimini un Tag principale, saltano anche tutti i suoi sinonimi)
                entity.HasOne(d => d.Tag)
                    .WithMany(p => p.Synonyms)
                    .HasForeignKey(d => d.TagId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_TagSynonyms_Tags");
            });

            // 1. Configurazione della cronologia di ricerca dell'IA
            modelBuilder.Entity<SearchHistory>(entity =>
            {
                entity.ToTable("SearchHistory");
                entity.HasKey(e => e.Id).HasName("PK_SearchHistory");

                entity.Property(e => e.Id).HasColumnName("Id");
                entity.Property(e => e.UserId).HasColumnName("UserId");
                entity.Property(e => e.QueryTags).HasColumnName("QueryTags").IsRequired();
                entity.Property(e => e.ResultContentId).HasColumnName("ResultContentId");
                entity.Property(e => e.IsSuccessful).HasColumnName("IsSuccessful").HasDefaultValue(false);
                entity.Property(e => e.Timestamp).HasColumnName("Timestamp");

                // Relazioni protette (Restrict) per evitare cicli di eliminazione
                entity.HasOne(d => d.User)
                    .WithMany()
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(d => d.ResultContent)
                    .WithMany()
                    .HasForeignKey(d => d.ResultContentId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // 2. Configurazione della tabella dei pesi statistici (Chiave Composta)
            modelBuilder.Entity<StatisticalWeights>(entity =>
            {
                entity.ToTable("StatisticalWeights");

                // Definiamo la chiave composta
                entity.HasKey(e => new { e.TagId, e.ContentId }).HasName("PK_StatisticalWeights");

                entity.Property(e => e.TagId).HasColumnName("TagId");
                entity.Property(e => e.ContentId).HasColumnName("ContentId");
                entity.Property(e => e.PopularityWeight).HasColumnName("PopularityWeight").HasDefaultValue(0);

                entity.HasOne(d => d.Tag)
                    .WithMany()
                    .HasForeignKey(d => d.TagId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(d => d.Content)
                    .WithMany()
                    .HasForeignKey(d => d.ContentId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // 3. Configurazione della tabella dei Log Amministrativi (Audit Log)
            modelBuilder.Entity<AdminLog>(entity =>
            {
                // Mappatura sulla tabella fisica al plurale
                entity.ToTable("AdminLogs");

                // Chiave primaria con indice pulito
                entity.HasKey(e => e.Id).HasName("PK_AdminLogs");

                // Allineamento colonne ed esplicitazione vincoli
                entity.Property(e => e.Id).HasColumnName("Id");
                entity.Property(e => e.UserId).HasColumnName("UserId");

                entity.Property(e => e.ActionType)
                    .HasMaxLength(100)
                    .HasColumnName("ActionType")
                    .IsRequired();

                entity.Property(e => e.Description)
                    .HasColumnName("Description")
                    .IsRequired();

                // Recupero automatico del timestamp su SQL Server con alta precisione
                entity.Property(e => e.ExecutedAt)
                    .HasPrecision(0)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP")
                    .HasColumnName("ExecutedAt");

                entity.Property(e => e.IpAddress)
                    .HasMaxLength(45) // 45 caratteri supportano perfettamente gli indirizzi IPv6 completi
                    .HasColumnName("IpAddress")
                    .IsRequired(false);

                // Relazione verso l'utente che ha compiuto l'azione.
                // Usiamo Restrict o ClientSetNull per non generare percorsi di eliminazione multipli a cascata su SQL Server.
                entity.HasOne(d => d.User)
                    .WithMany(p => p.AdminLogs)
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_AdminLogs_Users");
            });
        }
    }
}
