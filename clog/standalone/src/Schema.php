<?php

declare(strict_types=1);

namespace Clog\Standalone;

use Eleph\SQLite\Database;
use PDO;
use RuntimeException;

/** Installs the generated DDL. Evolving a populated database is a reviewed application migration. */
final class Schema
{
    public static function install(Database $db): void
    {
        if (self::isInstalled($db)) {
            return;
        }

        /** @var callable(PDO): void $install */
        $install = require dirname(__DIR__, 2) . '/generated/sqlite/install.php';
        $install($db->pdo);
    }

    public static function requireReady(Database $db): void
    {
        if (!self::isInstalled($db)) {
            throw new RuntimeException('Run `php standalone/cli.php install` before serving requests.');
        }
    }

    private static function isInstalled(Database $db): bool
    {
        return [] !== $db->select("SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'app_clog_item'");
    }
}
