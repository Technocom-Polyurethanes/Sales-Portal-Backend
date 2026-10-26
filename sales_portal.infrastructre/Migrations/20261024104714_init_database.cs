using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace sales_portal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class init_database : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bank_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank = table.Column<string>(type: "text", nullable: false),
                    branch = table.Column<string>(type: "text", nullable: false),
                    account_holder_name = table.Column<string>(type: "text", nullable: false),
                    account_number = table.Column<string>(type: "text", nullable: false),
                    swift = table.Column<string>(type: "text", nullable: false),
                    iban = table.Column<string>(type: "text", nullable: false),
                    currency = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bank_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "company_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    name_ar = table.Column<string>(type: "text", nullable: false),
                    tagline = table.Column<string>(type: "text", nullable: false),
                    tagline_ar = table.Column<string>(type: "text", nullable: false),
                    form_code = table.Column<string>(type: "text", nullable: false),
                    reference_prefix = table.Column<string>(type: "text", nullable: false),
                    address = table.Column<string>(type: "text", nullable: true),
                    phone = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    logo_url = table.Column<string>(type: "text", nullable: true),
                    logo_width_mm = table.Column<decimal>(type: "numeric", nullable: false),
                    logo_height_mm = table.Column<decimal>(type: "numeric", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_company_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "materials",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    erp_next_item_code = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    reference_price_per_kg = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    uom = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_materials", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "package_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name_ar = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_package_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "payment_terms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    label = table.Column<string>(type: "text", nullable: false),
                    label_ar = table.Column<string>(type: "text", nullable: false),
                    days = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payment_terms", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    username = table.Column<string>(type: "text", nullable: false),
                    full_name = table.Column<string>(type: "text", nullable: false),
                    initials = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: true),
                    role_title = table.Column<string>(type: "text", nullable: true),
                    role_title_ar = table.Column<string>(type: "text", nullable: true),
                    team = table.Column<string>(type: "text", nullable: true),
                    team_ar = table.Column<string>(type: "text", nullable: true),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    erp_next_id = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    name_ar = table.Column<string>(type: "text", nullable: true),
                    contact_person = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    phone = table.Column<string>(type: "text", nullable: true),
                    city = table.Column<string>(type: "text", nullable: true),
                    address = table.Column<string>(type: "text", nullable: false),
                    default_payment_term_id = table.Column<Guid>(type: "uuid", nullable: true),
                    default_currency = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customers", x => x.id);
                    table.ForeignKey(
                        name: "fk_customers_payment_terms_default_payment_term_id",
                        column: x => x.default_payment_term_id,
                        principalTable: "payment_terms",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "workspace_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    remind_before_expiry = table.Column<bool>(type: "boolean", nullable: false),
                    days_before_expiry = table.Column<int>(type: "integer", nullable: false),
                    default_currency = table.Column<string>(type: "text", nullable: false),
                    default_tax_percent = table.Column<decimal>(type: "numeric", nullable: false),
                    default_payment_term_id = table.Column<Guid>(type: "uuid", nullable: true),
                    default_document_language = table.Column<int>(type: "integer", nullable: false),
                    erp_next_url = table.Column<string>(type: "text", nullable: true),
                    erp_next_api_key = table.Column<string>(type: "text", nullable: true),
                    erp_next_api_secret = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workspace_settings", x => x.id);
                    table.ForeignKey(
                        name: "fk_workspace_settings_payment_terms_default_payment_term_id",
                        column: x => x.default_payment_term_id,
                        principalTable: "payment_terms",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "offers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    serial_number = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    parent_offer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attention = table.Column<string>(type: "text", nullable: true),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    payment_term_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency = table.Column<string>(type: "text", nullable: false),
                    document_language = table.Column<int>(type: "integer", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    form_code = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    order_discount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    tax_percent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    other_tax_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    other_tax_name = table.Column<string>(type: "text", nullable: false),
                    other_tax_percent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    other_tax_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    snapshot_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_offers", x => x.id);
                    table.ForeignKey(
                        name: "fk_offers_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_offers_offers_parent_offer_id",
                        column: x => x.parent_offer_id,
                        principalTable: "offers",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_offers_payment_terms_payment_term_id",
                        column: x => x.payment_term_id,
                        principalTable: "payment_terms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_offers_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "offer_delivery_batches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    offer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_number = table.Column<int>(type: "integer", nullable: false),
                    delivery_date = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_offer_delivery_batches", x => x.id);
                    table.ForeignKey(
                        name: "fk_offer_delivery_batches_offers_offer_id",
                        column: x => x.offer_id,
                        principalTable: "offers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "offer_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    offer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    material_id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_code = table.Column<string>(type: "text", nullable: false),
                    material_name = table.Column<string>(type: "text", nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    rate_per_kg = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    discount_percent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    quantity_kg = table.Column<decimal>(type: "numeric(18,3)", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    show_specifications = table.Column<bool>(type: "boolean", nullable: false),
                    custom_specifications = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_offer_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_offer_lines_materials_material_id",
                        column: x => x.material_id,
                        principalTable: "materials",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_offer_lines_offers_offer_id",
                        column: x => x.offer_id,
                        principalTable: "offers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "offer_payment_milestones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    offer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    phase_description = table.Column<string>(type: "text", nullable: false),
                    percentage_or_fixed = table.Column<decimal>(type: "numeric", nullable: false),
                    amount_due = table.Column<decimal>(type: "numeric", nullable: false),
                    due_date_or_event = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_offer_payment_milestones", x => x.id);
                    table.ForeignKey(
                        name: "fk_offer_payment_milestones_offers_offer_id",
                        column: x => x.offer_id,
                        principalTable: "offers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "offer_delivery_allocations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    delivery_batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_id = table.Column<Guid>(type: "uuid", nullable: false),
                    packages = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_offer_delivery_allocations", x => x.id);
                    table.ForeignKey(
                        name: "fk_offer_delivery_allocations_materials_material_id",
                        column: x => x.material_id,
                        principalTable: "materials",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_offer_delivery_allocations_offer_delivery_batches_delivery_",
                        column: x => x.delivery_batch_id,
                        principalTable: "offer_delivery_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "offer_line_packagings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    offer_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    package_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    package_type_name = table.Column<string>(type: "text", nullable: false),
                    package_type_name_ar = table.Column<string>(type: "text", nullable: false),
                    weight_per_package_kg = table.Column<decimal>(type: "numeric", nullable: false),
                    packages = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_offer_line_packagings", x => x.id);
                    table.ForeignKey(
                        name: "fk_offer_line_packagings_offer_lines_offer_line_id",
                        column: x => x.offer_line_id,
                        principalTable: "offer_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_offer_line_packagings_package_types_package_type_id",
                        column: x => x.package_type_id,
                        principalTable: "package_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_customers_customer_code",
                table: "customers",
                column: "customer_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_customers_default_payment_term_id",
                table: "customers",
                column: "default_payment_term_id");

            migrationBuilder.CreateIndex(
                name: "ix_materials_item_code",
                table: "materials",
                column: "item_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_offer_delivery_allocations_delivery_batch_id",
                table: "offer_delivery_allocations",
                column: "delivery_batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_offer_delivery_allocations_material_id",
                table: "offer_delivery_allocations",
                column: "material_id");

            migrationBuilder.CreateIndex(
                name: "ix_offer_delivery_batches_offer_id",
                table: "offer_delivery_batches",
                column: "offer_id");

            migrationBuilder.CreateIndex(
                name: "ix_offer_line_packagings_offer_line_id",
                table: "offer_line_packagings",
                column: "offer_line_id");

            migrationBuilder.CreateIndex(
                name: "ix_offer_line_packagings_package_type_id",
                table: "offer_line_packagings",
                column: "package_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_offer_lines_material_id",
                table: "offer_lines",
                column: "material_id");

            migrationBuilder.CreateIndex(
                name: "ix_offer_lines_offer_id",
                table: "offer_lines",
                column: "offer_id");

            migrationBuilder.CreateIndex(
                name: "ix_offer_payment_milestones_offer_id",
                table: "offer_payment_milestones",
                column: "offer_id");

            migrationBuilder.CreateIndex(
                name: "ix_offers_created_by_user_id",
                table: "offers",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_offers_customer_id",
                table: "offers",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_offers_parent_offer_id",
                table: "offers",
                column: "parent_offer_id");

            migrationBuilder.CreateIndex(
                name: "ix_offers_payment_term_id",
                table: "offers",
                column: "payment_term_id");

            migrationBuilder.CreateIndex(
                name: "ix_offers_reference",
                table: "offers",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_workspace_settings_default_payment_term_id",
                table: "workspace_settings",
                column: "default_payment_term_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bank_accounts");

            migrationBuilder.DropTable(
                name: "company_settings");

            migrationBuilder.DropTable(
                name: "offer_delivery_allocations");

            migrationBuilder.DropTable(
                name: "offer_line_packagings");

            migrationBuilder.DropTable(
                name: "offer_payment_milestones");

            migrationBuilder.DropTable(
                name: "workspace_settings");

            migrationBuilder.DropTable(
                name: "offer_delivery_batches");

            migrationBuilder.DropTable(
                name: "offer_lines");

            migrationBuilder.DropTable(
                name: "package_types");

            migrationBuilder.DropTable(
                name: "materials");

            migrationBuilder.DropTable(
                name: "offers");

            migrationBuilder.DropTable(
                name: "customers");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "payment_terms");
        }
    }
}
