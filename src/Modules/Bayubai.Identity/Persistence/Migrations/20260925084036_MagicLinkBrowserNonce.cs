using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bayubai.Identity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MagicLinkBrowserNonce : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BrowserNonceHash",
                schema: "identity",
                table: "magic_link_tokens",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BrowserNonceHash",
                schema: "identity",
                table: "magic_link_tokens");
        }
    }
}
