using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanitasStudios_WebApp.Migrations
{
    /// <inheritdoc />
    public partial class InitialIdentityAndSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tag",
                columns: table => new
                {
                    ID_T = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Tag_Name = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Type_T = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false, defaultValue: "articolo")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Tag__B87EA51889DF0D54", x => x.ID_T);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Content",
                columns: table => new
                {
                    ID_C = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Type_C = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false, defaultValue: "articolo"),
                    Title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Desc_C = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Data_Pub = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(getdate())"),
                    Data_Edit = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true),
                    Editor_ID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Content__B87EA50961BA4A46", x => x.ID_C);
                    table.ForeignKey(
                        name: "FK_Editor",
                        column: x => x.Editor_ID,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Promotion",
                columns: table => new
                {
                    ID_Promotion = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Promoted_ID = table.Column<int>(type: "int", nullable: false),
                    Admin_Promoter_ID = table.Column<int>(type: "int", nullable: false),
                    Data_Promotion = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Promotio__ECECECBEA1BEC634", x => x.ID_Promotion);
                    table.ForeignKey(
                        name: "FK_Admin_Promoter_ID",
                        column: x => x.Admin_Promoter_ID,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Promoted_ID",
                        column: x => x.Promoted_ID,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Comment",
                columns: table => new
                {
                    ID_Comm = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Comm_Text = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Data_Pub = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(getdate())"),
                    Content_ID = table.Column<int>(type: "int", nullable: false),
                    Comment_User_ID = table.Column<int>(type: "int", nullable: false),
                    Answer_ID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Comment__560C251E2B77EB82", x => x.ID_Comm);
                    table.ForeignKey(
                        name: "FK_Answer_ID",
                        column: x => x.Answer_ID,
                        principalTable: "Comment",
                        principalColumn: "ID_Comm");
                    table.ForeignKey(
                        name: "FK_Comment_User_ID",
                        column: x => x.Comment_User_ID,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Content_ID",
                        column: x => x.Content_ID,
                        principalTable: "Content",
                        principalColumn: "ID_C");
                });

            migrationBuilder.CreateTable(
                name: "Order",
                columns: table => new
                {
                    Content_Ord_ID = table.Column<int>(type: "int", nullable: false),
                    Tag_Ord_ID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Order__16EB5FFAF718821E", x => new { x.Content_Ord_ID, x.Tag_Ord_ID });
                    table.ForeignKey(
                        name: "FK_Content_Ord_ID",
                        column: x => x.Content_Ord_ID,
                        principalTable: "Content",
                        principalColumn: "ID_C");
                    table.ForeignKey(
                        name: "FK_Tag_Ord_ID",
                        column: x => x.Tag_Ord_ID,
                        principalTable: "Tag",
                        principalColumn: "ID_T");
                });

            migrationBuilder.CreateTable(
                name: "Section",
                columns: table => new
                {
                    ID_S = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Section_Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Order_num = table.Column<int>(type: "int", nullable: false),
                    Content_S_ID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Section__B87EA5193321E3F5", x => x.ID_S);
                    table.ForeignKey(
                        name: "FK_Content_S_ID",
                        column: x => x.Content_S_ID,
                        principalTable: "Content",
                        principalColumn: "ID_C",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Evaluate",
                columns: table => new
                {
                    User_Like_ID = table.Column<int>(type: "int", nullable: false),
                    Comm_Like_ID = table.Column<int>(type: "int", nullable: false),
                    isLike = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Evaluate__1F28091D4ECADA78", x => new { x.User_Like_ID, x.Comm_Like_ID });
                    table.ForeignKey(
                        name: "FK_Comm_Like_ID",
                        column: x => x.Comm_Like_ID,
                        principalTable: "Comment",
                        principalColumn: "ID_Comm");
                    table.ForeignKey(
                        name: "FK_User_Like_ID",
                        column: x => x.User_Like_ID,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Image",
                columns: table => new
                {
                    ID_I = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Image_Url = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Is_Thumbnail = table.Column<bool>(type: "bit", nullable: false),
                    Section_Image_ID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Image__B87EA503141B6819", x => x.ID_I);
                    table.ForeignKey(
                        name: "FK_Section_Image_ID",
                        column: x => x.Section_Image_ID,
                        principalTable: "Section",
                        principalColumn: "ID_S");
                });

            migrationBuilder.CreateTable(
                name: "Video",
                columns: table => new
                {
                    ID_V = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Video_Url = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Section_Video_ID = table.Column<int>(type: "int", nullable: false),
                    Image_Video_ID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Video__B87EA516CAC63B2A", x => x.ID_V);
                    table.ForeignKey(
                        name: "FK_Image_Video_ID",
                        column: x => x.Image_Video_ID,
                        principalTable: "Image",
                        principalColumn: "ID_I");
                    table.ForeignKey(
                        name: "FK_Section_Video_ID",
                        column: x => x.Section_Video_ID,
                        principalTable: "Section",
                        principalColumn: "ID_S");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Comment_Answer_ID",
                table: "Comment",
                column: "Answer_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Comment_Comment_User_ID",
                table: "Comment",
                column: "Comment_User_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Comment_Content_ID",
                table: "Comment",
                column: "Content_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Content_Editor_ID",
                table: "Content",
                column: "Editor_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Evaluate_Comm_Like_ID",
                table: "Evaluate",
                column: "Comm_Like_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Image_Section_Image_ID",
                table: "Image",
                column: "Section_Image_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Order_Tag_Ord_ID",
                table: "Order",
                column: "Tag_Ord_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Promotion_Admin_Promoter_ID",
                table: "Promotion",
                column: "Admin_Promoter_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Promotion_Promoted_ID",
                table: "Promotion",
                column: "Promoted_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Section_Content_S_ID",
                table: "Section",
                column: "Content_S_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Video_Section_Video_ID",
                table: "Video",
                column: "Section_Video_ID");

            migrationBuilder.CreateIndex(
                name: "UQ__Video__57417FA3F6E793E4",
                table: "Video",
                column: "Image_Video_ID",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "Evaluate");

            migrationBuilder.DropTable(
                name: "Order");

            migrationBuilder.DropTable(
                name: "Promotion");

            migrationBuilder.DropTable(
                name: "Video");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "Comment");

            migrationBuilder.DropTable(
                name: "Tag");

            migrationBuilder.DropTable(
                name: "Image");

            migrationBuilder.DropTable(
                name: "Section");

            migrationBuilder.DropTable(
                name: "Content");

            migrationBuilder.DropTable(
                name: "AspNetUsers");
        }
    }
}
