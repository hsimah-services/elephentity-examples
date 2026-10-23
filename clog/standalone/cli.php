<?php

declare(strict_types=1);

require __DIR__ . '/bootstrap.php';

use Clog\Standalone\Application;
use Clog\Standalone\GraphQL;
use Clog\Standalone\Schema;
use Clog\Standalone\Viewer;
use Eleph\SQLite\Database;
use GraphQL\Utils\SchemaPrinter;

try {
    $db = new Database(getenv('CLOG_DB') ?: 'clog.sqlite');

    switch ($argv[1] ?? '') {
        case 'install':
            Schema::install($db);
            echo "SQLite schema ready.\n";
            break;
        case 'schema':
            echo SchemaPrinter::doPrint(GraphQL::schema(new Application($db, new Viewer())));
            break;
        default:
            throw new RuntimeException('Commands: install | schema');
    }
} catch (Throwable $error) {
    fwrite(STDERR, $error->getMessage() . "\n");
    exit(1);
}
