<?php

declare(strict_types=1);

/*
 * Plugin Name: Clog
 * Description: The Elephentity example, as a WordPress plugin.
 * Requires PHP: 8.3
 */

namespace Clog;

use Eleph\WordPress\Database\WpdbDatabase;
use wpdb;

if (!defined('ABSPATH')) {
    exit;
}

require_once __DIR__ . '/vendor/autoload.php';

/**
 * WordPress lifecycle bindings: activation migrates, init registers types, before_delete_post
 * detaches post links, and plugins_loaded boots the runtime.
 */
function bootstrap(): Bootstrap
{
    /** @var Bootstrap|null $bootstrap */
    static $bootstrap = null;

    if (null !== $bootstrap) {
        return $bootstrap;
    }

    global $wpdb;

    assert($wpdb instanceof wpdb);

    return $bootstrap = new Bootstrap(new WpdbDatabase($wpdb));
}

register_activation_hook(__FILE__, static function (): void {
    $plan = bootstrap()->install();

    if (!$plan->isSafe()) {
        wp_die(esc_html(sprintf(
            "Clog could not migrate its tables:\n\n%s",
            implode("\n", array_map(static fn ($refusal): string => $refusal->describe(), $plan->refusals)),
        )));
    }
});

add_action('init', static function (): void {
    // WordPress requires post type registration on this hook and no earlier.
    bootstrap()->postTypes()->register();
});

add_action('admin_menu', static function (): void {
    add_menu_page('Clog', 'Clog', 'manage_options', 'clog', static function (): void {
        bootstrap()->adminPages()->render('Item');
    }, 'dashicons-database');
    bootstrap()->adminPages()->register();
});

add_action('before_delete_post', static function (int $postId): void {
    // The out-of-band case framework enforcement cannot see: someone emptying the
    // trash, or another plugin calling wp_delete_post().
    bootstrap()->orphanGuard()->onPostDeleted($postId);
});

add_action('plugins_loaded', static function (): void {
    bootstrap()->graphql()->boot();
});
